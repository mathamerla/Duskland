using System.Net.Http.Json;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Api.Modules.Products.Requests;
using Duskland.Modules.Products.Domain;

namespace Duskland.Api.IntegrationTests.Products;

public class CreateProductTests: IClassFixture<DusklandWebApplicationFactory>
{
    private readonly HttpClient _httpClient;

    public CreateProductTests(DusklandWebApplicationFactory factory)
    {
        _httpClient = factory.CreateClient();

        factory.ClearTable<Product>();
    }

    [Fact]
    public async Task CreateProduct_ValidRequest_Returns201Created()
    {
         var response = await _httpClient.PostAsJsonAsync("/api/stores/7b2a9d8c-4f1e-45b6-bd4e-e17f3a8b2c5d/products", 
            new CreateProductRequest("adas", "adsa", 10.0m));
        
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }
}
