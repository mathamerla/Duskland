using System.Net;
using System.Net.Http.Json;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Api.Modules.Inventory.Requests;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Products.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Duskland.Api.IntegrationTests.Inventory;

public class CreateStockTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;

    public CreateStockTests(DusklandWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
        factory.ClearTable<Stock>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public async Task CreateStock_ValidRequest_Returns201Created(int quantity)
    {
        var storeId = Guid.NewGuid();
        var productId = await InsertProductAsync(storeId);

        using var response = await CreateStockAsync(storeId, productId, quantity);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/stores/{storeId}/inventory/{productId}", response.Headers.Location?.OriginalString);
        var stock = Assert.Single(await _factory.GetTable<Stock>());
        Assert.NotEqual(Guid.Empty, stock.Id);
        Assert.Equal(storeId, stock.StoreId);
        Assert.Equal(productId, stock.ProductId);
        Assert.Equal(quantity, stock.Quantity);
        Assert.Equal(0, stock.ReservedQuantity);
        Assert.Equal(quantity, stock.AvailableQuantity);
        Assert.NotEqual(Guid.Empty, stock.Version);
    }

    [Fact]
    public async Task CreateStock_NegativeQuantity_Returns400WithoutPersistingStock()
    {
        var storeId = Guid.NewGuid();
        var productId = await InsertProductAsync(storeId);
        using var response = await CreateStockAsync(storeId, productId, -1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Equal("Quantity cannot be negative.", problem.Detail);
        Assert.Empty(await _factory.GetTable<Stock>());
    }

    [Fact]
    public async Task CreateStock_DuplicateStoreAndProduct_Returns409WithoutChangingStock()
    {
        var storeId = Guid.NewGuid();
        var productId = await InsertProductAsync(storeId);
        using var created = await CreateStockAsync(storeId, productId, 10);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = Assert.Single(await _factory.GetTable<Stock>());

        using var response = await CreateStockAsync(storeId, productId, 20);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Equal($"Product {productId} and store {storeId} is not unique for specified stock", problem.Detail);
        var stock = Assert.Single(await _factory.GetTable<Stock>());
        Assert.Equal(original.Id, stock.Id);
        Assert.Equal(10, stock.Quantity);
        Assert.Equal(0, stock.ReservedQuantity);
        Assert.Equal(original.Version, stock.Version);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateStock_DifferentStoreOrProduct_Returns201ForBoth(bool differentStore)
    {
        var storeId = Guid.NewGuid();
        var productId = await InsertProductAsync(storeId);
        var secondStoreId = differentStore ? Guid.NewGuid() : storeId;
        var secondProductId = await InsertProductAsync(secondStoreId);

        using var first = await CreateStockAsync(storeId, productId, 10);
        using var second = await CreateStockAsync(secondStoreId, secondProductId, 20);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var stocks = await _factory.GetTable<Stock>();
        Assert.Equal(2, stocks.Count);
        Assert.Contains(stocks, stock => stock.StoreId == storeId && stock.ProductId == productId && stock.Quantity == 10);
        Assert.Contains(stocks, stock => stock.StoreId == secondStoreId && stock.ProductId == secondProductId && stock.Quantity == 20);
    }

    [Fact]
    public async Task CreateStock_ConcurrentDuplicateRequests_OnlyOneSucceeds()
    {
        var storeId = Guid.NewGuid();
        var productId = await InsertProductAsync(storeId);

        var responses = await Task.WhenAll(
            CreateStockAsync(storeId, productId, 10),
            CreateStockAsync(storeId, productId, 10));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            var stock = Assert.Single(await _factory.GetTable<Stock>());
            Assert.Equal(storeId, stock.StoreId);
            Assert.Equal(productId, stock.ProductId);
            Assert.Equal(10, stock.Quantity);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Fact]
    public async Task CreateStock_NonexistentProduct_Returns404WithoutPersistingStock()
    {
        var productId = Guid.NewGuid();
        using var response = await CreateStockAsync(Guid.NewGuid(), productId, 10);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal($"Product with Id {productId} not found", problem.Detail);
        Assert.Empty(await _factory.GetTable<Stock>());
    }

    [Fact]
    public async Task CreateStock_ProductFromAnotherStore_Returns404WithoutPersistingStock()
    {
        var productId = await InsertProductAsync(Guid.NewGuid());

        using var response = await CreateStockAsync(Guid.NewGuid(), productId, 10);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal($"Product with Id {productId} not found", problem.Detail);
        Assert.Empty(await _factory.GetTable<Stock>());
    }

    private async Task<Guid> InsertProductAsync(Guid storeId)
    {
        var product = Product.Create(storeId, $"SKU-{Guid.NewGuid():N}", "Test product", 10m);
        await _factory.InsertIntoTable([product]);
        return product.Id;
    }

    private Task<HttpResponseMessage> CreateStockAsync(Guid storeId, Guid productId, int quantity)
        => _httpClient.PostAsJsonAsync($"/api/stores/{storeId}/inventory?productId={productId}",
            new CreateStockRequest(quantity));
}
