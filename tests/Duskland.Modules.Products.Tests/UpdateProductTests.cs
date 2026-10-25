using Duskland.Modules.Products.Domain;
using Duskland.Modules.Products.Domain.Common;

namespace Duskland.Modules.Products.Tests;

public class UpdateProductTests
{
    [Fact]
    public void Product_Create_AssignsNonEmptyVersion()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU-1", "Apple", 10m);

        Assert.NotEqual(Guid.Empty, product.Version);
    }

    [Theory]
    [InlineData("Updated apple", 25.50)]
    [InlineData("Free apple", 0)]
    public void UpdateDetails_ValidData_UpdatesDetailsAndVersion(string name, decimal price)
    {
        var product = Product.Create(Guid.NewGuid(), "SKU-1", "Apple", 10m);
        var originalVersion = product.Version;
        var originalIdentity = (product.Id, product.StoreId, product.SKU, product.IsActive, product.CreatedAt);

        product.UpdateDetails(name, price);

        Assert.Equal(name, product.Name);
        Assert.Equal(price, product.Price);
        Assert.NotEqual(Guid.Empty, product.Version);
        Assert.NotEqual(originalVersion, product.Version);
        Assert.Equal(originalIdentity, (product.Id, product.StoreId, product.SKU, product.IsActive, product.CreatedAt));
    }

    [Fact]
    public void UpdateDetails_UnchangedDetails_ChangesVersion()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU-1", "Apple", 10m);
        var originalVersion = product.Version;

        product.UpdateDetails(product.Name, product.Price);

        Assert.NotEqual(originalVersion, product.Version);
    }

    [Theory]
    [InlineData(null, "Product name can't be empty")]
    [InlineData("", "Product name can't be empty")]
    [InlineData("   ", "Product name can't be empty")]
    [InlineData("ab", "Prodcut name need to be between 3 and 200 characters")]
    public void UpdateDetails_InvalidName_ThrowsWithoutChangingVersion(string? name, string expectedMessage)
    {
        var product = Product.Create(Guid.NewGuid(), "SKU-1", "Apple", 10m);
        var originalVersion = product.Version;

        var exception = Assert.Throws<DomainException>(() => product.UpdateDetails(name!, 20m));

        Assert.Equal(expectedMessage, exception.Message);

        Assert.Equal("Apple", product.Name);
        Assert.Equal(10m, product.Price);
        Assert.Equal(originalVersion, product.Version);
    }

    [Fact]
    public void UpdateDetails_TooLongName_ThrowsWithoutChangingVersion()
    {
        var product = Product.Create(Guid.NewGuid(), "SKU-1", "Apple", 10m);
        var originalVersion = product.Version;

        var exception = Assert.Throws<DomainException>(() => product.UpdateDetails(new string('a', 201), 20m));

        Assert.Equal("Prodcut name need to be between 3 and 200 characters", exception.Message);

        Assert.Equal(originalVersion, product.Version);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void UpdateDetails_NegativePrice_ThrowsWithoutChangingNamePriceOrVersion(decimal invalidPrice)
    {
        var product = Product.Create(Guid.NewGuid(), "SKU-1", "Apple", 10m);
        var originalVersion = product.Version;

        var originalName = product.Name;
        var originalPrice = product.Price;

        var exception = Assert.Throws<DomainException>(() => product.UpdateDetails("Updated apple", invalidPrice));

        Assert.Equal("Product price can't be below zero", exception.Message);

        Assert.Equal(originalName, product.Name);
        Assert.Equal(originalPrice, product.Price);
        Assert.Equal(originalVersion, product.Version);
    }
}
