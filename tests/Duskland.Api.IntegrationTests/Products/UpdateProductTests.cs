using System.Net;
using System.Net.Http.Json;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Api.Modules.Products.Requests;
using Duskland.Modules.Products.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Duskland.Api.IntegrationTests.Products;

public class UpdateProductTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly Guid _storeId = Guid.NewGuid();

    public UpdateProductTests(DusklandWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
        factory.ClearTable<Product>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateProduct_CurrentVersion_Returns204AndPersistsDetails(bool quotedVersion)
    {
        var product = await SeedProductAsync();
        var version = quotedVersion ? $"\"{product.Version}\"" : product.Version.ToString();

        using var response = await PatchAsync(product, version);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        var saved = Assert.Single(await _factory.GetTable<Product>());
        Assert.Equal("Updated apple", saved.Name);
        Assert.Equal(25m, saved.Price);
        Assert.NotEqual(product.Version, saved.Version);
        Assert.NotEqual(Guid.Empty, saved.Version);
        Assert.Equal(product.Id, saved.Id);
        Assert.Equal(product.StoreId, saved.StoreId);
        Assert.Equal(product.SKU, saved.SKU);
        Assert.Equal(product.IsActive, saved.IsActive);
        Assert.Equal(product.CreatedAt, saved.CreatedAt);
    }

    [Theory]
    [InlineData(null, "Missing version in the request")]
    [InlineData("not-a-guid", "Incorrect format of the version in the request")]
    [InlineData("*", "Incorrect format of the version in the request")]
    [InlineData("W/\"00000000-0000-0000-0000-000000000001\"", "Incorrect format of the version in the request")]
    public async Task UpdateProduct_MissingOrInvalidVersion_Returns412WithoutChanges(string? version, string expectedDetail)
    {
        var product = await SeedProductAsync();

        using var response = await PatchAsync(product, version);

        await AssertProblemAsync(response, HttpStatusCode.PreconditionFailed, expectedDetail);
        await AssertUnchangedAsync(product);
    }

    [Fact]
    public async Task UpdateProduct_StaleVersion_Returns412WithoutChanges()
    {
        var product = await SeedProductAsync();

        using var response = await PatchAsync(product, $"\"{Guid.NewGuid()}\"");

        await AssertProblemAsync(response, HttpStatusCode.PreconditionFailed,
            "Version of the product is different than defined in request");
        await AssertUnchangedAsync(product);
    }

    [Fact]
    public async Task UpdateProduct_MissingProduct_Returns404()
    {
        var product = Product.Create(_storeId, "SKU-1", "Apple", 10m);

        using var response = await PatchAsync(product, $"\"{product.Version}\"");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, $"Product with Id {product.Id} not found");
        Assert.Empty(await _factory.GetTable<Product>());
    }

    [Theory]
    [InlineData("", 25, "Product name can't be empty")]
    [InlineData("  ", 25, "Product name can't be empty")]
    [InlineData("ab", 25, "Prodcut name need to be between 3 and 200 characters")]
    [InlineData("Updated apple", -1, "Product price can't be below zero")]
    public async Task UpdateProduct_InvalidDetails_Returns400WithoutPersistingChanges(string name, decimal price, string expectedDetail)
    {
        var product = await SeedProductAsync();

        using var response = await PatchAsync(product, $"\"{product.Version}\"", name: name, price: price);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, expectedDetail);
        await AssertUnchangedAsync(product);
    }

    [Fact]
    public async Task UpdateProduct_ReusePreviousVersion_Returns412AndPreservesFirstUpdate()
    {
        var product = await SeedProductAsync();
        var version = $"\"{product.Version}\"";
        using var firstResponse = await PatchAsync(product, version);
        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);
        var firstUpdate = Assert.Single(await _factory.GetTable<Product>());

        using var secondResponse = await PatchAsync(product, version, name: "Second update", price: 50m);

        await AssertProblemAsync(secondResponse, HttpStatusCode.PreconditionFailed,
            "Version of the product is different than defined in request");
        await AssertUnchangedAsync(firstUpdate);
    }

    [Fact]
    public async Task UpdateProduct_ConcurrentRequestsWithSameVersion_OnlyOneSucceeds()
    {
        var product = await SeedProductAsync();
        var version = $"\"{product.Version}\"";

        var responses = await Task.WhenAll(
            PatchAsync(product, version, name: "First update", price: 20m),
            PatchAsync(product, version, name: "Second update", price: 30m));

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            var failedResponse = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.PreconditionFailed);
            await AssertProblemAsync(failedResponse, HttpStatusCode.PreconditionFailed,
                "Version of the product is different than defined in request");
            var saved = Assert.Single(await _factory.GetTable<Product>());
            Assert.Contains((saved.Name, saved.Price), new[] { ("First update", 20m), ("Second update", 30m) });
            Assert.NotEqual(product.Version, saved.Version);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private async Task<Product> SeedProductAsync()
    {
        var product = Product.Create(_storeId, "SKU-1", "Apple", 10m);
        await _factory.InsertIntoTable([product]);
        // Read PostgreSQL's stored timestamp precision for later persistence comparisons.
        return Assert.Single(await _factory.GetTable<Product>());
    }

    private async Task<HttpResponseMessage> PatchAsync(
        Product product, string? version, Guid? storeId = null, string name = "Updated apple", decimal price = 25m)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/stores/{storeId ?? _storeId}/products/{product.Id}")
        {
            Content = JsonContent.Create(new UpdateProductRequest(name, price))
        };
        if (version is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", version);
        }

        return await _httpClient.SendAsync(request);
    }

    private async Task AssertUnchangedAsync(Product expected)
    {
        var saved = Assert.Single(await _factory.GetTable<Product>());
        Assert.Equivalent(expected, saved);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedDetail)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Equal(expectedDetail, problem.Detail);
    }
}
