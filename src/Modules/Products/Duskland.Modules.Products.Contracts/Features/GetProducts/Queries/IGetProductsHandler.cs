using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;

namespace Duskland.Modules.Products.Contracts.Features.GetProducts.Queries;

public interface IGetProductsHandler
{
    Task<PagedList<ProductDto>> Handle(GetProductsQuery query, CancellationToken token);
}
