using System.Net;
using System.Net.Http.Json;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Api.Modules.Products.Requests;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;
using Duskland.Modules.Products.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Duskland.Api.IntegrationTests.Products;

public class GetProductTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly Guid _storeId = Guid.NewGuid();

    public GetProductTests(DusklandWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
        factory.ClearTable<Product>();
    }

    [Fact]
    public async Task GetProduct_ExistingProduct_Returns200WithDetailsAndVersionHeader()
    {
        var product = Product.Create(_storeId, "SKU-1", "Apple", 12.50m);
        var otherProduct = Product.Create(_storeId, "SKU-2", "Banana", 20m);
        await _factory.InsertIntoTable([otherProduct, product]);

        using var response = await _httpClient.GetAsync(BuildUrl(product.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(result);
        Assert.Equal(new ProductDto(product.Id, product.StoreId, product.SKU, product.Name, product.Price), result);
        Assert.Equal(product.Version, GetVersion(response));
    }

    [Fact]
    public async Task GetProduct_MissingProduct_Returns404WithMessage()
    {
        var existingProduct = Product.Create(_storeId, "SKU-1", "Apple", 10m);
        await _factory.InsertIntoTable([existingProduct]);
        var missingId = Guid.NewGuid();

        using var response = await _httpClient.GetAsync(BuildUrl(missingId));

        await AssertNotFoundAsync(response, missingId);
    }


    [Fact]
    public async Task GetProduct_AfterUpdate_ReturnsUpdatedDetailsAndNewVersion()
    {
        var product = Product.Create(_storeId, "SKU-1", "Apple", 10m);
        await _factory.InsertIntoTable([product]);
        using var initialResponse = await _httpClient.GetAsync(BuildUrl(product.Id));
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);
        var originalVersion = GetVersion(initialResponse);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/stores/{_storeId}/products")
        {
            Content = JsonContent.Create(new UpdateProductRequest(product.Id, product.SKU, "Updated apple", 25m))
        };
        request.Headers.TryAddWithoutValidation("If-Match", Assert.Single(initialResponse.Headers.GetValues("ETag")));
        using var updateResponse = await _httpClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        using var response = await _httpClient.GetAsync(BuildUrl(product.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(result);
        Assert.Equal(new ProductDto(product.Id, _storeId, product.SKU, "Updated apple", 25m), result);
        var saved = Assert.Single(await _factory.GetTable<Product>());
        var version = GetVersion(response);
        Assert.NotEqual(originalVersion, version);
        Assert.Equal(saved.Version, version);
    }

    [Fact]
    public async Task GetProduct_CreateLocation_ReturnsCreatedProduct()
    {
        using var createResponse = await _httpClient.PostAsJsonAsync($"/api/stores/{_storeId}/products",
            new CreateProductRequest("SKU-1", "Apple", 10m));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        using var response = await _httpClient.GetAsync(createResponse.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(result);
        var saved = Assert.Single(await _factory.GetTable<Product>());
        Assert.Equal(new ProductDto(saved.Id, _storeId, "SKU-1", "Apple", 10m), result);
        Assert.Equal(saved.Version, GetVersion(response));
    }

    private string BuildUrl(Guid productId) => $"/api/stores/{_storeId}/products/{productId}/";

    private static Guid GetVersion(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("ETag", out var values));
        var header = Assert.Single(values);
        Assert.True(Guid.TryParse(header.Trim('"'), out var version));
        Assert.NotEqual(Guid.Empty, version);
        return version;
    }

    private static async Task AssertNotFoundAsync(HttpResponseMessage response, Guid productId)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal($"Product with Id {productId} not found", problem.Detail);
        Assert.False(response.Headers.Contains("ETag"));
    }
}
