namespace Duskland.Modules.Products.Contracts.Features.CreateProduct.Command;

public record CreateProductCommand(Guid StoreId, string SKU, string Name, decimal Price);