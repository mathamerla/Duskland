using System.Net;
using System.Net.Http.Json;
using System.Text;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;
using Duskland.Modules.Products.Contracts.Features.GetProductsKeysetPagination.Dtos;
using Duskland.Modules.Products.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Duskland.Api.IntegrationTests.Products;

public class GetProductsKeysetPaginationTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly Guid _storeId = Guid.NewGuid();

    public GetProductsKeysetPaginationTests(DusklandWebApplicationFactory factory)
    {
        _factory = factory;
        _httpClient = factory.CreateClient();
        factory.ClearTable<Product>();
    }

    [Fact]
    public async Task GetProductsKeyset_NoQueryParameters_ReturnsFirstPageWithDefaultPageSize()
    {
        var products = Enumerable.Range(1, 21)
            .Select(index => Product.Create(_storeId, $"SKU-{index}", $"Product {index}", index))
            .Reverse()
            .ToArray();
        await _factory.InsertIntoTable(products);

        using var response = await _httpClient.GetAsync($"/api/stores/{_storeId}/products/scroll/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<KeysetPagedList<ProductDto>>();
        Assert.NotNull(page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(20, page.Items.Count());
        Assert.Equal(products.OrderBy(p => p.Price).Take(20).Select(p => p.Id), page.Items.Select(p => p.Id));
        Assert.True(page.HasMore);
        Assert.False(string.IsNullOrWhiteSpace(page.NextCursor));

        var nextPage = await GetPageAsync(page.PageSize, page.NextCursor);

        Assert.Equal(products.Single(p => p.Price == 21m).Id, Assert.Single(nextPage.Items).Id);
        Assert.False(nextPage.HasMore);
        Assert.Equal(string.Empty, nextPage.NextCursor);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(6, false)]
    [InlineData(100, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(6, true)]
    [InlineData(100, true)]
    public async Task GetProductsKeyset_ConsecutivePages_ReturnAllProductsInOrderWithoutDuplicates(
        int pageSize, bool identicalPrices)
    {
        var prices = new[] { 30m, 10m, 20m, 10m, 30m, 10m };
        var products = prices.Select((price, index) =>
            Product.Create(_storeId, $"SKU-{index}", $"Product {index}", identicalPrices ? 12.50m : price))
            .ToArray();
        var otherStoreProduct = Product.Create(Guid.NewGuid(), "OTHER", "Other store product", 12.50m);
        await _factory.InsertIntoTable(products.Append(otherStoreProduct).ToArray());

        var expected = products.OrderBy(p => p.Price).ThenBy(p => p.Id).ToArray();
        var receivedIds = new List<Guid>();
        var cursors = new HashSet<string>();
        var cursor = string.Empty;

        for (var offset = 0; offset < expected.Length; offset += pageSize)
        {
            var page = await GetPageAsync(pageSize, cursor);
            var expectedPage = expected.Skip(offset).Take(pageSize).ToArray();
            var hasMore = offset + pageSize < expected.Length;

            Assert.Equal(pageSize, page.PageSize);
            Assert.Equal(expectedPage.Select(p => p.Id), page.Items.Select(p => p.Id));
            Assert.All(page.Items, item => Assert.Equal(_storeId, item.StoreId));
            Assert.Equal(hasMore, page.HasMore);
            if (hasMore)
            {
                Assert.False(string.IsNullOrWhiteSpace(page.NextCursor));
                Assert.True(cursors.Add(page.NextCursor), "Each page must advance the cursor.");
            }
            else
            {
                Assert.Equal(string.Empty, page.NextCursor);
            }

            receivedIds.AddRange(page.Items.Select(p => p.Id));
            cursor = page.NextCursor;
        }

        Assert.Equal(expected.Select(p => p.Id), receivedIds);
        Assert.Equal(products.Length, receivedIds.Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GetProductsKeyset_NoMoreThanPageSizeProducts_ReturnsPageWithoutNextCursor(int count)
    {
        var products = Enumerable.Range(0, count)
            .Select(index => Product.Create(_storeId, $"SKU-{index}", $"Product {index}", 10m))
            .ToArray();
        await _factory.InsertIntoTable(products);

        var page = await GetPageAsync(2);

        Assert.Equal(2, page.PageSize);
        Assert.Equal(products.OrderBy(p => p.Id).Select(p => p.Id), page.Items.Select(p => p.Id));
        Assert.False(page.HasMore);
        Assert.Equal(string.Empty, page.NextCursor);
    }

    [Fact]
    public async Task GetProductsKeyset_ProductsAfterCursorDeleted_ReturnsEmptyFinalPage()
    {
        var products = Enumerable.Range(0, 3)
            .Select(index => Product.Create(_storeId, $"SKU-{index}", $"Product {index}", 10m))
            .ToArray();
        await _factory.InsertIntoTable(products);
        var firstPage = await GetPageAsync(2);
        Assert.True(firstPage.HasMore);
        _factory.ClearTable<Product>();

        var page = await GetPageAsync(2, firstPage.NextCursor);

        Assert.Empty(page.Items);
        Assert.False(page.HasMore);
        Assert.Equal(string.Empty, page.NextCursor);
    }

    [Theory]
    [InlineData("%%%")]
    [InlineData("a")]
    [InlineData("invalid cursor!")]
    public async Task GetProductsKeyset_InvalidBase64UrlCursor_Returns400BadRequest(string cursor)
    {
        await AssertInvalidCursorAsync(cursor);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"Price\":")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"Price\":10}")]
    [InlineData("{\"Id\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"Price\":\"invalid\",\"Id\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"Price\":10,\"Id\":\"invalid\"}")]
    [InlineData("{\"Price\":null,\"Id\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData("{\"Price\":10,\"Id\":null}")]
    public async Task GetProductsKeyset_Base64UrlCursorWithInvalidPayload_Returns400BadRequest(string payload)
    {
        var cursor = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload));

        await AssertInvalidCursorAsync(cursor);
    }

    private async Task AssertInvalidCursorAsync(string cursor)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/stores/{_storeId}/products/scroll/?PageSize=2&Cursor={Uri.EscapeDataString(cursor)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Equal("Invalid cursor format.", problem.Detail);
    }

    private async Task<KeysetPagedList<ProductDto>> GetPageAsync(int pageSize, string cursor = "")
    {
        using var response = await _httpClient.GetAsync(
            $"/api/stores/{_storeId}/products/scroll/?PageSize={pageSize}&Cursor={Uri.EscapeDataString(cursor)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<KeysetPagedList<ProductDto>>();
        Assert.NotNull(page);
        return page;
    }
}
