using Duskland.Modules.Products.Contracts.Common;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;

namespace Duskland.Modules.Products.Tests;

public class PagedQueryParametersTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("\t\r\n", false)]
    [InlineData("Name", true)]
    [InlineData(" Name ", true)]
    public void HasSorting_SortBy_ReturnsExpectedResult(string? sortBy, bool expected)
    {
        var parameters = new PagedQueryParameters(1, 10, Direction.ASC, sortBy);

        Assert.Equal(expected, parameters.HasSorting);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(1000, 90)]
    public void CalculateSkip_CorrectSkip_ReturnsExpectedResult(int page, int pageSize)
    {
        var parameters = new PagedQueryParameters(page, pageSize, Direction.ASC, null);

        Assert.Equal((page - 1) * pageSize, parameters.SkipCount());
    }

    [Fact]
    public void CalculateSkip_OverflowSkip_ReturnsException()
    {
        var parameters = new PagedQueryParameters(3, int.MaxValue, Direction.ASC, null);

        var exception = Assert.Throws<ContractValidationException>(() => parameters.SkipCount());

        Assert.Equal("This page is not supported for that page size", exception.Message);

    }
}
