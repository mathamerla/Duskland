using Duskland.BuildingBlocks.Application.Requests;

namespace Duskland.Modules.Products.Tests;

public class KeysetPagedQueryParametersTests
{
    [Fact]
    public void Constructor_NoArguments_UsesDefaultValues()
    {
        var parameters = new KeysetPagedQueryParameters();

        Assert.Equal(string.Empty, parameters.Cursor);
        Assert.Equal(20, parameters.PageSize);
    }
}
