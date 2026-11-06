using System.Net;
using System.Net.Http.Json;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Api.Modules.Inventory.Requests;
using Duskland.Modules.Inventory.Contracts.Features.GetStock.Dtos;
using Duskland.Modules.Inventory.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Duskland.Api.IntegrationTests.Inventory;

public class RemoveStockTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly Guid _storeId = Guid.NewGuid();

    public RemoveStockTests(DusklandWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
        factory.ClearTable<Stock>();
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(0, 10)]
    [InlineData(3, 4)]
    [InlineData(3, 7)]
    public async Task RemoveStock_AvailableQuantity_Returns204AndPreservesReservations(int reserved, int quantity)
    {
        var stock = await SeedStockAsync(reserved);

        using var response = await RemoveAsync(_storeId, stock.ProductId, quantity);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        var saved = Assert.Single(await _factory.GetTable<Stock>());
        Assert.Equal(stock.Id, saved.Id);
        Assert.Equal(stock.StoreId, saved.StoreId);
        Assert.Equal(stock.ProductId, saved.ProductId);
        Assert.Equal(10 - quantity, saved.Quantity);
        Assert.Equal(reserved, saved.ReservedQuantity);
        Assert.Equal(10 - quantity - reserved, saved.AvailableQuantity);
        Assert.NotEqual(Guid.Empty, saved.Version);
        Assert.NotEqual(stock.Version, saved.Version);

        using var getResponse = await _httpClient.GetAsync($"/api/stores/{_storeId}/inventory/{stock.ProductId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(new StockDto(saved.Id, saved.StoreId, saved.ProductId,
            saved.Quantity, saved.ReservedQuantity, saved.AvailableQuantity),
            await getResponse.Content.ReadFromJsonAsync<StockDto>());
        Assert.Equal($"\"{saved.Version}\"", getResponse.Headers.ETag?.Tag);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task RemoveStock_NonPositiveQuantity_Returns400WithoutChanges(int quantity)
    {
        var stock = await SeedStockAsync();

        using var response = await RemoveAsync(_storeId, stock.ProductId, quantity);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Stock to remove must be greater than zero.");
        Assert.Equivalent(stock, Assert.Single(await _factory.GetTable<Stock>()));
    }

    [Theory]
    [InlineData(0, 11)]
    [InlineData(0, int.MaxValue)]
    [InlineData(3, 8)]
    [InlineData(10, 1)]
    public async Task RemoveStock_InsufficientAvailableQuantity_Returns400WithoutChanges(int reserved, int quantity)
    {
        var stock = await SeedStockAsync(reserved);

        using var response = await RemoveAsync(_storeId, stock.ProductId, quantity);

        await AssertInsufficientStockAsync(response, stock.ProductId, quantity);
        Assert.Equivalent(stock, Assert.Single(await _factory.GetTable<Stock>()));
    }

    [Fact]
    public async Task RemoveStock_MissingStock_Returns404WithoutCreatingStock()
    {
        var productId = Guid.NewGuid();

        using var response = await RemoveAsync(_storeId, productId, 5);

        await AssertProblemAsync(response, HttpStatusCode.NotFound,
            $"Stock for product {productId} in store {_storeId} does not exist.");
        Assert.Empty(await _factory.GetTable<Stock>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemoveStock_WrongStoreOrProduct_Returns404WithoutChanges(bool wrongStore)
    {
        var stock = await SeedStockAsync();
        var storeId = wrongStore ? Guid.NewGuid() : _storeId;
        var productId = wrongStore ? stock.ProductId : Guid.NewGuid();

        using var response = await RemoveAsync(storeId, productId, 5);

        await AssertProblemAsync(response, HttpStatusCode.NotFound,
            $"Stock for product {productId} in store {storeId} does not exist.");
        Assert.Equivalent(stock, Assert.Single(await _factory.GetTable<Stock>()));
    }

    [Fact]
    public async Task RemoveStock_ExistingStock_UpdatesOnlyRequestedStoreAndProduct()
    {
        var stock = await SeedStockAsync();
        var otherStore = Stock.Create(Guid.NewGuid(), stock.ProductId, 20);
        var otherProduct = Stock.Create(_storeId, Guid.NewGuid(), 30);
        await _factory.InsertIntoTable([otherStore, otherProduct]);

        using var response = await RemoveAsync(_storeId, stock.ProductId, 5);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stocks = await _factory.GetTable<Stock>();
        Assert.Equal(3, stocks.Count);
        Assert.Equal(5, Assert.Single(stocks, item => item.Id == stock.Id).Quantity);
        Assert.Equivalent(otherStore, Assert.Single(stocks, item => item.Id == otherStore.Id));
        Assert.Equivalent(otherProduct, Assert.Single(stocks, item => item.Id == otherProduct.Id));
    }

    [Theory]
    [InlineData(3, 2, 2, 6)]
    [InlineData(3, 5, 1, 5)]
    public async Task RemoveStock_ConcurrentRequests_PreservesAvailableStock(
        int reserved, int quantity, int expectedSuccesses, int expectedQuantity)
    {
        var stock = await SeedStockAsync(reserved);

        var responses = await Task.WhenAll(
            RemoveAsync(_storeId, stock.ProductId, quantity),
            RemoveAsync(_storeId, stock.ProductId, quantity));
        try
        {
            Assert.Equal(expectedSuccesses, responses.Count(response => response.StatusCode == HttpStatusCode.NoContent));
            foreach (var response in responses.Where(response => response.StatusCode != HttpStatusCode.NoContent))
                await AssertInsufficientStockAsync(response, stock.ProductId, quantity);
            var saved = Assert.Single(await _factory.GetTable<Stock>());
            Assert.Equal(expectedQuantity, saved.Quantity);
            Assert.Equal(reserved, saved.ReservedQuantity);
            Assert.Equal(expectedQuantity - reserved, saved.AvailableQuantity);
            Assert.NotEqual(stock.Version, saved.Version);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    private async Task<Stock> SeedStockAsync(int reserved = 0)
    {
        var stock = Stock.Create(_storeId, Guid.NewGuid(), 10);
        // Reservations have no public mutation method yet; seed state as EF would when loading stock.
        typeof(Stock).GetProperty(nameof(Stock.ReservedQuantity))!.SetValue(stock, reserved);
        await _factory.InsertIntoTable([stock]);
        return stock;
    }

    private Task<HttpResponseMessage> RemoveAsync(Guid storeId, Guid productId, int quantity)
        => _httpClient.PostAsJsonAsync($"/api/stores/{storeId}/inventory/{productId}/remove/",
            new RemoveStockRequest(quantity));

    private Task AssertInsufficientStockAsync(HttpResponseMessage response, Guid productId, int quantity)
        => AssertProblemAsync(response, HttpStatusCode.BadRequest,
            $"Removing {quantity} from stock for product {productId} in store {_storeId} would result in a negative quantity.");

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string detail)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)status, problem.Status);
        Assert.Equal(detail, problem.Detail);
    }
}
