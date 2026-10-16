using Duskland.Modules.Products.Domain.Common;

namespace Duskland.Modules.Products.Domain;

public class Product
{
    protected Product() 
    {
        //For ef core    
        Name = string.Empty;
        SKU = string.Empty;
    }

    public static Product CreateProduct(Guid storeId, string sku, string name, decimal price)
    {
        var product = new Product()
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        product.ChangeStoreId(storeId);
        product.ChangeName(name);
        product.ChangePrice(price);
        product.ChangeSKU(sku);

        return product;
    }

    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string SKU { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void ChangeStoreId(Guid storeId)
    {
        if (storeId == default)
        {
            throw new DomainException("Incorrect StoreId for Product");    
        }

        StoreId = storeId;
    }

    public void ChangeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product name can't be empty");    
        }

        if (name.Length > 200 || name.Length < 3)
        {
            throw new DomainException("Prodcut name need to be between 2 and 200 characters");    
        }

        Name = name;
    }

    public void ChangeSKU(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException("Product SKU can't be empty");    
        }

        if (sku.Length > 50 || sku.Length < 1)
        {
            throw new DomainException("Prodcut SKU need to be between 2 and 200 characters");    
        }

        SKU = sku;
    }

    public void ChangePrice(decimal price)
    {
        if (price < 0m)
        {
            throw new DomainException("Product price can't be below zero");    
        }

        Price = price; 
    }
}
