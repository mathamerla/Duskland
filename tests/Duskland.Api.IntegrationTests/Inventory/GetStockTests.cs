using System.Net;
using System.Net.Http.Json;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Api.Modules.Inventory.Requests;
using Duskland.Modules.Inventory.Contracts.Features.GetStock.Dtos;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Products.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Duskland.Api.IntegrationTests.Inventory;

public class GetStockTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly Guid _storeId = Guid.NewGuid();

    public GetStockTests(DusklandWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
        factory.ClearTable<Stock>();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 0)]
    [InlineData(10, 3)]
    [InlineData(10, 10)]
    public async Task GetStock_ExistingStock_Returns200WithDetailsAndVersionHeader(int quantity, int reserved)
    {
        var stock = Stock.Create(_storeId, Guid.NewGuid(), quantity);
        // Reservations have no public mutation method yet; seed state as EF would when loading stock.
        typeof(Stock).GetProperty(nameof(Stock.ReservedQuantity))!.SetValue(stock, reserved);
        var otherProductStock = Stock.Create(_storeId, Guid.NewGuid(), 20);
        var otherStoreStock = Stock.Create(Guid.NewGuid(), stock.ProductId, 30);
        await _factory.InsertIntoTable([otherProductStock, otherStoreStock, stock]);

        using var response = await _httpClient.GetAsync(BuildUrl(stock.ProductId));

        await AssertStockAsync(response, stock);
    }

    [Fact]
    public async Task GetStock_MissingProduct_Returns404WithMessage()
    {
        await _factory.InsertIntoTable([Stock.Create(_storeId, Guid.NewGuid(), 10)]);
        var productId = Guid.NewGuid();

        using var response = await _httpClient.GetAsync(BuildUrl(productId));

        await AssertNotFoundAsync(response, productId);
    }

    [Fact]
    public async Task GetStock_OtherStore_Returns404WithMessage()
    {
        var stock = Stock.Create(Guid.NewGuid(), Guid.NewGuid(), 10);
        await _factory.InsertIntoTable([stock]);

        using var response = await _httpClient.GetAsync(BuildUrl(stock.ProductId));

        await AssertNotFoundAsync(response, stock.ProductId);
    }

    [Fact]
    public async Task GetStock_ProductWithoutStock_Returns404WithMessage()
    {
        var product = Product.Create(_storeId, $"SKU-{Guid.NewGuid():N}", "Test product", 10m);
        await _factory.InsertIntoTable([product]);

        using var response = await _httpClient.GetAsync(BuildUrl(product.Id));

        await AssertNotFoundAsync(response, product.Id);
    }

    [Fact]
    public async Task GetStock_CreateLocation_ReturnsCreatedStock()
    {
        var product = Product.Create(_storeId, $"SKU-{Guid.NewGuid():N}", "Test product", 10m);
        await _factory.InsertIntoTable([product]);
        using var created = await _httpClient.PostAsJsonAsync(
            $"/api/stores/{_storeId}/inventory?productId={product.Id}", new CreateStockRequest(10));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        using var response = await _httpClient.GetAsync(created.Headers.Location);

        await AssertStockAsync(response, Assert.Single(await _factory.GetTable<Stock>()));
    }

    private string BuildUrl(Guid productId) => $"/api/stores/{_storeId}/inventory/{productId}/";

    private static async Task AssertStockAsync(HttpResponseMessage response, Stock stock)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StockDto>();
        Assert.NotNull(result);
        Assert.Equal(new StockDto(stock.Id, stock.StoreId, stock.ProductId,
            stock.Quantity, stock.ReservedQuantity, stock.AvailableQuantity), result);
        Assert.NotNull(response.Headers.ETag);
        Assert.False(response.Headers.ETag.IsWeak);
        Assert.Equal($"\"{stock.Version}\"", response.Headers.ETag.Tag);
    }

    private async Task AssertNotFoundAsync(HttpResponseMessage response, Guid productId)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal($"Stock for product {productId} and store {_storeId} not found", problem.Detail);
        Assert.False(response.Headers.Contains("ETag"));
    }
}
