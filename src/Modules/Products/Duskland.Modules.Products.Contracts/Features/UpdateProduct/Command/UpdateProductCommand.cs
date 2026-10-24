using System;

namespace Duskland.Modules.Products.Contracts.Features.UpdateProduct.Command;

public record UpdateProductCommand(Guid Id, Guid StoreId, string Name, decimal Price, Guid Version);
