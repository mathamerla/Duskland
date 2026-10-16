using Duskland.Modules.Products.Domain;
using Duskland.Modules.Products.Domain.Common;

namespace Duskland.Modules.Products.Tests;

public class ProductTests
{
    private readonly Guid _storeId = Guid.NewGuid();
    private readonly string _sku = "TEST";
    private readonly string _name = "TEST";
    private readonly decimal _price = 11.11m;

    [Fact]
    public void Product_ValidData_ProductCreated()
    {
        var product = Product.CreateProduct(_storeId, _sku, _name, _price);
    
        Assert.NotNull(product);
        Assert.Equal(_storeId, product.StoreId);
        Assert.Equal(_name, product.Name);
        Assert.Equal(_sku, product.SKU);
        Assert.Equal(_price, product.Price);
    }

    [Fact]
    public void Product_NegativePrice_Exception()
    {
        Assert.Throws<DomainException>(() 
            => Product.CreateProduct(_storeId, _sku, _name, -11.11m));
    }

    [Fact]
    public void Product_EmptyStoreID_Exception()
    {
        Assert.Throws<DomainException>(() 
            => Product.CreateProduct(default, _sku, _name, _price));
    }

    [Fact]
    public void Product_TooShortName_Exception()
    {
        Assert.Throws<DomainException>(() 
            => Product.CreateProduct(default, _sku, "aa", _price));
    }

    [Fact]
    public void Product_TooLongSKU_Exception()
    {
        Assert.Throws<DomainException>(() 
            => Product.CreateProduct(default, "aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789aBcDeFgHiJkLmNo", _name, _price));
    }

    [Fact]
    public void Product_NewProduct_IsActive()
    {
        var product = Product.CreateProduct(_storeId, _sku, _name, _price);

        Assert.True(product.IsActive);
    }

    [Fact]
    public void Product_ChangePrice_PriceChanged()
    {
        var product = Product.CreateProduct(_storeId, _sku, _name, _price);

        product.ChangePrice(155.1m);

        Assert.Equal(155.1m, product.Price);
    }


    [Fact]
    public void Product_ChangePrice_Exception()
    {
        var product = Product.CreateProduct(_storeId, _sku, _name, _price);

        Assert.Throws<DomainException>(() 
            => product.ChangePrice(-11.32m));    
    }
}
