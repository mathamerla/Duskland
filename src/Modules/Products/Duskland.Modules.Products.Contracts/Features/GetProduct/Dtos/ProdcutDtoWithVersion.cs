using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;

namespace Duskland.Modules.Products.Contracts.Features.GetProduct.Dtos;

public record ProductDtoWithVersion(ProductDto Product, Guid Version);