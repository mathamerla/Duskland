using System.Net.Http.Json;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Api.Modules.Products.Requests;
using Duskland.Modules.Products.Domain;

namespace Duskland.Api.IntegrationTests.Products;

public class CreateProductTests: IClassFixture<DusklandWebApplicationFactory>
{
    private readonly HttpClient _httpClient;
    private readonly DusklandWebApplicationFactory _factory;

    public CreateProductTests(DusklandWebApplicationFactory factory)
    {
        _httpClient = factory.CreateClient();
        _factory = factory;

        factory.ClearTable<Product>();
    }

    [Fact]
    public async Task CreateProduct_ValidRequest_Returns201Created()
    {
         var response = await _httpClient.PostAsJsonAsync("/api/stores/7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5d/products", 
            new CreateProductRequest("adas", "adsa", 10.0m));
        
        var products = await _factory.GetTable<Product>();

        var product = products.FirstOrDefault(c => c.Name == "adsa");
        var expected = GetProductToCompare("7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5d", "adas", "adsa", 10.0m );

        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(product);
        Assert.Equivalent(expected, product);
    }

    [Fact]
    public async Task CreateProduct_DuplicateSku_ReturnsConflict()
    {
        var cretedResponse = await _httpClient.PostAsJsonAsync("/api/stores/7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5d/products", 
            new CreateProductRequest("adas", "adsa", 10.0m));
        
        var conflictResponse = await _httpClient.PostAsJsonAsync("/api/stores/7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5d/products", 
            new CreateProductRequest("adas", "adsa1", 10.0m));

        Assert.Equal(System.Net.HttpStatusCode.Created, cretedResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_SameSkuDifferentStores_ReturnsCreated()
    {
        var productAResponse = await _httpClient.PostAsJsonAsync("/api/stores/7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5d/products", 
            new CreateProductRequest("adas", "ProductA", 10.0m));
        
        var productBResponse = await _httpClient.PostAsJsonAsync("/api/stores/7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5e/products", 
            new CreateProductRequest("adas", "ProductB", 10.0m));
        
        var products = await _factory.GetTable<Product>();

        var productA = products.FirstOrDefault(c => c.Name == "ProductA");
        var productB = products.FirstOrDefault(c => c.Name == "ProductB");

        var expectedA = GetProductToCompare("7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5d", "adas", "ProductA", 10.0m );
        var expectedB = GetProductToCompare("7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5e", "adas", "ProductB", 10.0m );

        Assert.Equal(System.Net.HttpStatusCode.Created, productAResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Created, productBResponse.StatusCode);

        Assert.NotNull(productA);
        Assert.NotNull(productB);

        Assert.Equivalent(expectedA, productA);
        Assert.Equivalent(expectedB, productB);
    }

    [Fact]
    public async Task CreateProduct_InvalidPrice_ReturnsBadRequest()
    {
        var response = await _httpClient.PostAsJsonAsync("/api/stores/7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5d/products", 
            new CreateProductRequest("adas", "adsa", -10.0m));

        var products = await _factory.GetTable<Product>();

        var product = products.FirstOrDefault(c => c.Name == "adsa");
        
        Assert.Null(product);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_ConcurrentRequests_OnlyOneSucceeds()
    {
        var requestA = _httpClient.PostAsJsonAsync("/api/stores/7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5d/products", 
            new CreateProductRequest("adas", "adsa", 10.0m));
        
        var requestB = _httpClient.PostAsJsonAsync("/api/stores/7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5d/products", 
            new CreateProductRequest("adas", "adsa1", 10.0m));

        var responses = await Task.WhenAll([requestA, requestB]);

        var products = await _factory.GetTable<Product>();

        Assert.Equal(1, products.Count());
        Assert.Single(responses, c => c.StatusCode == System.Net.HttpStatusCode.Created);
        Assert.Single(responses, c => c.StatusCode == System.Net.HttpStatusCode.Conflict);
    }

    private static dynamic GetProductToCompare(string storeId, string sku, string name, decimal price)
    {
        return new 
        { 
            StoreId = Guid.Parse(storeId), 
            SKU = sku, 
            Name = name, 
            Price = price 
        };
    }
}
