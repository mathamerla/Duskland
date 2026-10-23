using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Queries;
using Duskland.Modules.Products.Domain.Common;
using Duskland.Modules.Products.Features.GetProducts.Validators;

namespace Duskland.Modules.Products.Tests;

public class GetProductsValidatorTests
{
    private readonly Guid _storeId = Guid.NewGuid();

    [Theory]
    [InlineData("price")]
    [InlineData("sku")]
    [InlineData("name")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ValidSorting_DoesNotThrow(string? sortBy)
    {
        var query = new GetProductsQuery(new PagedQueryParameters(1, 10, Direction.ASC, sortBy), _storeId);

        var exception = Record.Exception(() => GetProductsValidator.Validate(query));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_InvalidStoreId_ThrowValidationException()
    {
        var query = new GetProductsQuery(new PagedQueryParameters(1, 10, Direction.ASC, "name"), Guid.Empty);

        var exception = Record.Exception(() => GetProductsValidator.Validate(query));

        Assert.Equal("StoreId can't be empty", exception.Message);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("Price")]
    [InlineData(" name ")]
    public void Validate_InvalidSorting_ThrowsValidationException(string sortBy)
    {
        var query = new GetProductsQuery(new PagedQueryParameters(1, 10, Direction.ASC, sortBy),_storeId);

        var exception = Assert.Throws<DomainException>(() => GetProductsValidator.Validate(query));

        Assert.Equal("Wrong sorting defined", exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_ValidPageSize_DoesNotThrow(int pageSize)
    {
        var query = new GetProductsQuery(new PagedQueryParameters(1, pageSize, Direction.ASC, "name"), _storeId);

        var exception = Record.Exception(() => GetProductsValidator.Validate(query));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_InvalidPageSize_ThrowsValidationException(int pageSize)
    {
        var query = new GetProductsQuery(new PagedQueryParameters(1, pageSize, Direction.ASC, "name"), _storeId);

        var exception = Assert.Throws<DomainException>(() => GetProductsValidator.Validate(query));

        Assert.Equal("The page size needs to be between 1 and 100", exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_ValidPage_DoesNotThrow(int page)
    {
        var query = new GetProductsQuery(new PagedQueryParameters(page, 10, Direction.ASC, "name"), _storeId);

        var exception = Record.Exception(() => GetProductsValidator.Validate(query));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Validate_InvalidPage_ThrowsValidationException(int page)
    {
        var query = new GetProductsQuery(new PagedQueryParameters(page, 10, Direction.ASC, "name"), _storeId);

        var exception = Assert.Throws<DomainException>(() => GetProductsValidator.Validate(query));

        Assert.Equal("The page can't be lower than 1", exception.Message);
    }
}
