using System.Net;
using System.Net.Http.Json;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;
using Duskland.Modules.Products.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Duskland.Api.IntegrationTests.Products;

public class GetProductsTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly Guid _storeId = Guid.NewGuid();

    public GetProductsTests(DusklandWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
        factory.ClearTable<Product>();
    }

    [Fact]
    public async Task GetProducts_NoProducts_ReturnsEmptyPage()
    {
        var result = await GetPageAsync();

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetProducts_ExistingProduct_ReturnsProductDetails()
    {
        var product = Product.Create(_storeId, "SKU-1", "Apple", 12.50m);
       
        await _factory.InsertIntoTable([product]);
        var test = await _factory.GetTable<Product>();

        var result = await GetPageAsync();

        Assert.Equal(1, result.TotalCount);

        var item = Assert.Single(result.Items);
        Assert.Equal(product.Id, item.Id);
        Assert.Equal(product.StoreId, item.StoreId);
        Assert.Equal(product.SKU, item.SKU);
        Assert.Equal(product.Name, item.Name);
        Assert.Equal(product.Price, item.Price);
    }

    [Theory]
    [InlineData("name", Direction.ASC)]
    [InlineData("name", Direction.DESC)]
    [InlineData("sku", Direction.ASC)]
    [InlineData("sku", Direction.DESC)]
    [InlineData("price", Direction.ASC)]
    [InlineData("price", Direction.DESC)]
    public async Task GetProducts_Sorting_ReturnsProductsInRequestedOrder(string sortBy, Direction direction)
    {
        var apple = Product.Create(_storeId, "SKU-C", "Apple", 20m);
        var banana = Product.Create(_storeId, "SKU-A", "Banana", 30m);
        var cherry = Product.Create(_storeId, "SKU-B", "Cherry", 10m);
        
        await _factory.InsertIntoTable([banana, cherry, apple]);
        
        var ascending = sortBy switch
        {
            "name" => [apple.SKU, banana.SKU, cherry.SKU],
            "sku" => [banana.SKU, cherry.SKU, apple.SKU],
            _ => new[] { cherry.SKU, apple.SKU, banana.SKU }
        };

        var expected = direction == Direction.ASC 
            ? ascending 
            : [.. ascending.Reverse()];

        var result = await GetPageAsync(sortBy: sortBy, direction: direction);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(expected, result.Items.Select(p => p.SKU));
    }

    [Fact]
    public async Task GetProducts_NoQueryParams_ReturnsForDefaultParams()
    {
        var apple = Product.Create(_storeId, "SKU-C", "Apple", 20m);
        var banana = Product.Create(_storeId, "SKU-A", "Banana", 30m);
        var cherry = Product.Create(_storeId, "SKU-B", "Cherry", 10m);
        
        await _factory.InsertIntoTable([banana, cherry, apple]);
        
        var expected = new List<string> { apple.SKU, banana.SKU, cherry.SKU };

        var response = await _httpClient.GetAsync(_baseUrl(_storeId));
        var result = await response.Content.ReadFromJsonAsync<PagedList<ProductDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(expected, result.Items.Select(p => p.SKU));
    }

    [Theory]
    [InlineData(1, 2, 2)]
    [InlineData(2, 2, 1)]
    [InlineData(3, 2, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(2, 1, 1)]
    [InlineData(1, 100, 3)]
    public async Task GetProducts_Pagination_ReturnsRequestedPage(int page, int pageSize, int expectedCount)
    {
        var products = new[]
        {
            Product.Create(_storeId, "SKU-1", "Apple", 10m),
            Product.Create(_storeId, "SKU-2", "Banana", 20m),
            Product.Create(_storeId, "SKU-3", "Cherry", 30m)
        };
        
        await _factory.InsertIntoTable(products);
                
        var expected = products.Reverse().Skip((page - 1) * pageSize).Take(pageSize).Select(p => p.Id);
        var result = await GetPageAsync(page, pageSize, "name", Direction.DESC);

        Assert.Equal(page, result.Page);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(expectedCount, result.Items.Count);
        Assert.Equal(expected, result.Items.Select(p => p.Id));
    }

    [Theory]
    [InlineData("aPpLe", 2)]
    [InlineData("%APPLE%", 0)]
    [InlineData("missing", 0)]
    public async Task GetProducts_Search_ReturnsMatchingNames(string searchBy, int expectedCount)
    {
        var products = new []
        {
            Product.Create(_storeId, "SKU-1", "Apple", 10m),
            Product.Create(_storeId, "SKU-2", "Pineapple", 20m),
            Product.Create(_storeId, "SKU-3", "Banana", 30m)
        };

        await _factory.InsertIntoTable(products);

        var result = await GetPageAsync(searchBy: searchBy);

        Assert.Equal(expectedCount, result.TotalCount);
        Assert.Equal(expectedCount, result.Items.Count);
        Assert.All(result.Items, item => Assert.Contains("apple", item.Name.ToLowerInvariant()));
    }

    [Fact]
    public async Task GetProducts_OtherStoreProducts_ReturnsOnlyRequestedStoreProducts()
    {
        var product = Product.Create(_storeId, "SKU-1", "Apple", 10m);
        var productFromOtherStore = Product.Create(Guid.NewGuid(), "SKU-1", "Apple", 10m);
        await _factory.InsertIntoTable([product, productFromOtherStore]);

        var result = await GetPageAsync();

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(product.Id, Assert.Single(result.Items).Id);
    }

    [Theory]
    [InlineData("unknown", 10, "Wrong sorting defined")]
    [InlineData("name", 0, "The page size needs to be between 1 and 100")]
    [InlineData("name", -1, "The page size needs to be between 1 and 100")]
    [InlineData("name", 101, "The page size needs to be between 1 and 100")]
    public async Task GetProducts_InvalidParameters_Returns400BadRequest(string sortBy, int pageSize, string expectedDetail)
    {
        using var response = await _httpClient.GetAsync(BuildUrl(1, pageSize, sortBy, Direction.ASC, ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Equal(expectedDetail, problem.Detail);
    }

    private async Task<PagedList<ProductDto>> GetPageAsync(
        int page = 1, int pageSize = 10, string sortBy = "", Direction direction = Direction.ASC, string searchBy = "")
    {
        var response = await _httpClient.GetAsync(BuildUrl(page, pageSize, sortBy, direction, searchBy));
        var result = await response.Content.ReadFromJsonAsync<PagedList<ProductDto>>();
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        
        return result;
    }

    private string _baseUrl(Guid storeId) => $"/api/stores/{storeId}/products";

    private string BuildUrl(int page, int pageSize, string sortBy, Direction direction, string searchBy)
        => $"{_baseUrl(_storeId)}?Page={page}&PageSize={pageSize}"
           + $"&SortBy={Uri.EscapeDataString(sortBy)}&Direction={direction}&SearchBy={Uri.EscapeDataString(searchBy)}";
}
