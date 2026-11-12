using System.Net;
using System.Net.Http.Json;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Api.Modules.Inventory.Requests;
using Duskland.Modules.Inventory.Contracts.Features.GetStock.Dtos;
using Duskland.Modules.Inventory.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Duskland.Api.IntegrationTests.Inventory;

public class SetQuantityTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly Guid _storeId = Guid.NewGuid();

    public SetQuantityTests(DusklandWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
        factory.ClearTable<Stock>();
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3, 3, false)]
    [InlineData(3, 5, true)]
    [InlineData(3, 10, false)]
    [InlineData(3, int.MaxValue, true)]
    public async Task SetQuantity_CurrentVersion_Returns204AndReplacesQuantity(int reserved, int quantity, bool quotedVersion)
    {
        var stock = await SeedStockAsync(reserved);
        var version = quotedVersion ? $"\"{stock.Version}\"" : stock.Version.ToString();

        using var response = await PatchAsync(stock, quantity, version);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        var saved = Assert.Single(await _factory.GetTable<Stock>());
        Assert.Equal(stock.Id, saved.Id);
        Assert.Equal(stock.StoreId, saved.StoreId);
        Assert.Equal(stock.ProductId, saved.ProductId);
        Assert.Equal(quantity, saved.Quantity);
        Assert.Equal(reserved, saved.ReservedQuantity);
        Assert.Equal(quantity - reserved, saved.AvailableQuantity);
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
    [InlineData(-1, "Quantity cannot be negative.")]
    [InlineData(int.MinValue, "Quantity cannot be negative.")]
    [InlineData(0, "Quantity cannot be lower than reserved quantity.")]
    [InlineData(2, "Quantity cannot be lower than reserved quantity.")]
    public async Task SetQuantity_InvalidQuantity_Returns400WithoutChanges(int quantity, string detail)
    {
        var stock = await SeedStockAsync(3);

        using var response = await PatchAsync(stock, quantity, stock.Version.ToString());

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, detail);
        await AssertUnchangedAsync(stock);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.PreconditionRequired, "Missing version in the request")]
    [InlineData("not-a-guid", HttpStatusCode.PreconditionFailed, "Incorrect format of the version in the request")]
    [InlineData("*", HttpStatusCode.PreconditionFailed, "Incorrect format of the version in the request")]
    [InlineData("W/\"00000000-0000-0000-0000-000000000001\"", HttpStatusCode.PreconditionFailed, "Incorrect format of the version in the request")]
    public async Task SetQuantity_MissingOrInvalidVersion_ReturnsProblemWithoutChanges(
        string? version, HttpStatusCode status, string detail)
    {
        var stock = await SeedStockAsync();

        using var response = await PatchAsync(stock, 5, version);

        await AssertProblemAsync(response, status, detail);
        await AssertUnchangedAsync(stock);
    }

    [Fact]
    public async Task SetQuantity_StaleVersion_Returns412WithoutChanges()
    {
        var stock = await SeedStockAsync();

        using var response = await PatchAsync(stock, 5, Guid.NewGuid().ToString());

        await AssertVersionConflictAsync(response);
        await AssertUnchangedAsync(stock);
    }

    [Fact]
    public async Task SetQuantity_MissingStock_Returns404WithoutCreatingStock()
    {
        var stock = Stock.Create(_storeId, Guid.NewGuid(), 10);

        using var response = await PatchAsync(stock, 5, stock.Version.ToString());

        await AssertProblemAsync(response, HttpStatusCode.NotFound, $"Stock with Product Id {stock.ProductId} not found");
        Assert.Empty(await _factory.GetTable<Stock>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetQuantity_WrongStoreOrProduct_Returns404WithoutChanges(bool wrongStore)
    {
        var stock = await SeedStockAsync();
        var storeId = wrongStore ? Guid.NewGuid() : stock.StoreId;
        var productId = wrongStore ? stock.ProductId : Guid.NewGuid();

        using var response = await PatchAsync(stock, 5, stock.Version.ToString(), storeId, productId);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, $"Stock with Product Id {productId} not found");
        await AssertUnchangedAsync(stock);
    }

    [Fact]
    public async Task SetQuantity_ExistingStock_UpdatesOnlyRequestedStoreAndProduct()
    {
        var stock = await SeedStockAsync();
        var otherStore = Stock.Create(Guid.NewGuid(), stock.ProductId, 20);
        var otherProduct = Stock.Create(_storeId, Guid.NewGuid(), 30);
        await _factory.InsertIntoTable([otherStore, otherProduct]);

        using var response = await PatchAsync(stock, 5, stock.Version.ToString());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stocks = await _factory.GetTable<Stock>();
        Assert.Equal(3, stocks.Count);
        Assert.Equal(5, Assert.Single(stocks, item => item.Id == stock.Id).Quantity);
        Assert.Equivalent(otherStore, Assert.Single(stocks, item => item.Id == otherStore.Id));
        Assert.Equivalent(otherProduct, Assert.Single(stocks, item => item.Id == otherProduct.Id));
    }

    [Fact]
    public async Task SetQuantity_ReusePreviousVersion_Returns412AndPreservesFirstUpdate()
    {
        var stock = await SeedStockAsync();
        using var firstResponse = await PatchAsync(stock, 5, stock.Version.ToString());
        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);
        var firstUpdate = Assert.Single(await _factory.GetTable<Stock>());

        using var secondResponse = await PatchAsync(stock, 20, stock.Version.ToString());

        await AssertVersionConflictAsync(secondResponse);
        await AssertUnchangedAsync(firstUpdate);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(3, 7)]
    public async Task SetQuantity_OldETagAfterAddingStock_Returns412AndPreservesAddedStock(int reserved, int quantityToAdd)
    {
        var stock = await SeedStockAsync(reserved);
        using var getResponse = await _httpClient.GetAsync($"/api/stores/{_storeId}/inventory/{stock.ProductId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var oldETag = getResponse.Headers.ETag?.Tag;
        Assert.NotNull(oldETag);

        using var addResponse = await _httpClient.PostAsJsonAsync(
            $"/api/stores/{_storeId}/inventory/{stock.ProductId}/add/",
            new AddStockRequest(quantityToAdd));
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);
        var addedStock = Assert.Single(await _factory.GetTable<Stock>());
        Assert.Equal(10 + quantityToAdd, addedStock.Quantity);
        Assert.Equal(reserved, addedStock.ReservedQuantity);
        Assert.Equal(10 + quantityToAdd - reserved, addedStock.AvailableQuantity);
        Assert.NotEqual(stock.Version, addedStock.Version);

        using var response = await PatchAsync(stock, 5, oldETag);

        await AssertVersionConflictAsync(response);
        await AssertUnchangedAsync(addedStock);
    }

    [Fact]
    public async Task SetQuantity_ConcurrentRequestsWithSameVersion_OnlyOneSucceeds()
    {
        var stock = await SeedStockAsync(3);
        var responses = await Task.WhenAll(
            PatchAsync(stock, 5, stock.Version.ToString()),
            PatchAsync(stock, 20, stock.Version.ToString()));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            await AssertVersionConflictAsync(Assert.Single(responses,
                response => response.StatusCode == HttpStatusCode.PreconditionFailed));
            var saved = Assert.Single(await _factory.GetTable<Stock>());
            Assert.Contains(saved.Quantity, new[] { 5, 20 });
            Assert.Equal(3, saved.ReservedQuantity);
            Assert.Equal(saved.Quantity - 3, saved.AvailableQuantity);
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

    private async Task<HttpResponseMessage> PatchAsync(
        Stock stock, int quantity, string? version, Guid? storeId = null, Guid? productId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/stores/{storeId ?? stock.StoreId}/inventory/{productId ?? stock.ProductId}/quantity")
        {
            Content = JsonContent.Create(new SetQuantityRequest(quantity))
        };
        if (version is not null)
            request.Headers.TryAddWithoutValidation("If-Match", version);
        return await _httpClient.SendAsync(request);
    }

    private async Task AssertUnchangedAsync(Stock expected)
        => Assert.Equivalent(expected, Assert.Single(await _factory.GetTable<Stock>()));

    private static Task AssertVersionConflictAsync(HttpResponseMessage response)
        => AssertProblemAsync(response, HttpStatusCode.PreconditionFailed,
            "Version of the stock is different than defined in request");

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string detail)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)status, problem.Status);
        Assert.Equal(detail, problem.Detail);
    }
}
