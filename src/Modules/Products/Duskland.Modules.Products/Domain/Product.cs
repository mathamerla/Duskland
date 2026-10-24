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

    public static Product Create(Guid storeId, string sku, string name, decimal price)
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
        product.ElevateVersion();
        
        return product;
    }

    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string SKU { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid Version { get; private set; }

    public void ChangePrice(decimal price)
    {
        if (price < 0m)
        {
            throw new DomainException("Product price can't be below zero");    
        }

        Price = price; 
    }

    public void ChangeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product name can't be empty");    
        }

        if (name.Length > 200 || name.Length < 3)
        {
            throw new DomainException("Prodcut name need to be between 3 and 200 characters");    
        }

        Name = name;
    }

    public void UpdateDetails(string name, decimal price)
    {
        ChangeName(name);
        ChangePrice(price);
        ElevateVersion();
    }

    private void ChangeStoreId(Guid storeId)
    {
        if (storeId == default)
        {
            throw new DomainException("Incorrect StoreId for Product");    
        }

        StoreId = storeId;
    }

    private void ChangeSKU(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException("Product SKU can't be empty");    
        }

        if (sku.Length > 50 || sku.Length < 1)
        {
            throw new DomainException("Product SKU need to be between 1 and 50 characters");    
        }

        SKU = sku;
    }

    private void ElevateVersion()
    {
        Version = Guid.NewGuid();
    }
}
