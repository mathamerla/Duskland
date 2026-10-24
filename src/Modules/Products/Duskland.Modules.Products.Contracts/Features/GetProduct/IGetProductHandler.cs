using Duskland.Modules.Products.Contracts.Features.GetProduct.Dtos;

namespace Duskland.Modules.Products.Contracts.Features.GetProduct;

public interface IGetProductHandler
{
    Task<ProductDtoWithVersion> Handle(GetProductQuery query, CancellationToken token);
}
