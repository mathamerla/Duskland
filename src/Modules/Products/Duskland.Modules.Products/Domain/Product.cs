using System;

namespace Duskland.Modules.Products.Domain;

public class Product
{
    protected Product() 
    {
        //For ef core    
    }

    public Product(Guid storeId, string sku, string name, decimal price)
    {
        Id = Guid.NewGuid();
        StoreId = storeId;
        SKU = sku;
        Name = name;
        Price = price;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;

    }

    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string SKU { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
