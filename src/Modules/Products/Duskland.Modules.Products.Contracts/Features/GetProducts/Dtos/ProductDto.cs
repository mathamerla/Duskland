using System;

namespace Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;

public record ProductDto(Guid Id, Guid StoreId, string SKU, string Name, decimal Price);