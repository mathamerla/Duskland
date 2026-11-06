using System.Net;
using System.Net.Http.Json;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Api.Modules.Inventory.Requests;
using Duskland.Modules.Inventory.Contracts.Features.GetStock.Dtos;
using Duskland.Modules.Inventory.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Duskland.Api.IntegrationTests.Inventory;

public class AddStockTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly Guid _storeId = Guid.NewGuid();

    public AddStockTests(DusklandWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
        factory.ClearTable<Stock>();
    }

    [Theory]
    [InlineData(5)]
    public async Task AddStock_ValidQuantity_Returns201AndPreservesReservations(int quantity)
    {
        var stock = Stock.Create(_storeId, Guid.NewGuid(), 10);
        await _factory.InsertIntoTable([stock]);

        using var response = await AddAsync(_storeId, stock.ProductId, quantity);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/stores/{_storeId}/inventory/{stock.ProductId}", response.Headers.Location?.OriginalString);
        var saved = Assert.Single(await _factory.GetTable<Stock>());
        Assert.Equal(stock.Id, saved.Id);
        Assert.Equal(stock.StoreId, saved.StoreId);
        Assert.Equal(stock.ProductId, saved.ProductId);
        Assert.Equal(10 + quantity, saved.Quantity);
        Assert.NotEqual(Guid.Empty, saved.Version);
        Assert.NotEqual(stock.Version, saved.Version);

        using var getResponse = await _httpClient.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var result = await getResponse.Content.ReadFromJsonAsync<StockDto>();
        Assert.Equal(new StockDto(saved.Id, saved.StoreId, saved.ProductId,
            saved.Quantity, saved.ReservedQuantity, saved.AvailableQuantity), result);
        Assert.Equal($"\"{saved.Version}\"", getResponse.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task AddStock_ExistingStock_UpdatesOnlyRequestedStoreAndProduct()
    {
        var stock = Stock.Create(_storeId, Guid.NewGuid(), 10);
        var otherStore = Stock.Create(Guid.NewGuid(), stock.ProductId, 20);
        var otherProduct = Stock.Create(_storeId, Guid.NewGuid(), 30);
        await _factory.InsertIntoTable([stock, otherStore, otherProduct]);

        using var response = await AddAsync(_storeId, stock.ProductId, 5);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stocks = await _factory.GetTable<Stock>();
        Assert.Equal(3, stocks.Count);
        Assert.Equal(15, Assert.Single(stocks, item => item.Id == stock.Id).Quantity);
        Assert.Equivalent(otherStore, Assert.Single(stocks, item => item.Id == otherStore.Id));
        Assert.Equivalent(otherProduct, Assert.Single(stocks, item => item.Id == otherProduct.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddStock_WrongStoreOrProduct_Returns404WithoutChanges(bool wrongStore)
    {
        var stock = Stock.Create(_storeId, Guid.NewGuid(), 10);
        await _factory.InsertIntoTable([stock]);
        var storeId = wrongStore ? Guid.NewGuid() : _storeId;
        var productId = wrongStore ? stock.ProductId : Guid.NewGuid();

        using var response = await AddAsync(storeId, productId, 5);

        await AssertProblemAsync(response, HttpStatusCode.NotFound,
            $"Stock for product {productId} in store {storeId} does not exist.");
        Assert.Equivalent(stock, Assert.Single(await _factory.GetTable<Stock>()));
    }

    [Fact]
    public async Task AddStock_MissingStock_Returns404WithoutCreatingStock()
    {
        var productId = Guid.NewGuid();

        using var response = await AddAsync(_storeId, productId, 5);

        await AssertProblemAsync(response, HttpStatusCode.NotFound,
            $"Stock for product {productId} in store {_storeId} does not exist.");
        Assert.Empty(await _factory.GetTable<Stock>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-11)]
    public async Task AddStock_NegativeQuantity_Returns400WithoutChanges(int quantity)
    {
        var stock = Stock.Create(_storeId, Guid.NewGuid(), 10);
        await _factory.InsertIntoTable([stock]);

        using var response = await AddAsync(_storeId, stock.ProductId, quantity);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Stock to add must be greater than zero.");
        Assert.Equivalent(stock, Assert.Single(await _factory.GetTable<Stock>()));
    }

    [Fact]
    public async Task AddStock_ConcurrentRequests_PersistsBothIncrements()
    {
        var stock = Stock.Create(_storeId, Guid.NewGuid(), 10);
        await _factory.InsertIntoTable([stock]);

        var responses = await Task.WhenAll(
            AddAsync(_storeId, stock.ProductId, 5),
            AddAsync(_storeId, stock.ProductId, 7));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
            var saved = Assert.Single(await _factory.GetTable<Stock>());
            Assert.Equal(22, saved.Quantity);
            Assert.Equal(0, saved.ReservedQuantity);
            Assert.Equal(22, saved.AvailableQuantity);
            Assert.NotEqual(stock.Version, saved.Version);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    private Task<HttpResponseMessage> AddAsync(Guid storeId, Guid productId, int quantity)
        => _httpClient.PostAsJsonAsync($"/api/stores/{storeId}/inventory/{productId}/add/",
            new AddStockRequest(quantity));

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string detail)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)status, problem.Status);
        Assert.Equal(detail, problem.Detail);
    }
}
