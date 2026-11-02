using System;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;
using Duskland.Modules.Products.Contracts.Features.GetProductsKeysetPagination.Dtos;

namespace Duskland.Modules.Products.Contracts.Features.GetProductsKeysetPagination.Queries;

public interface IGetProductsKeysetPaginationHandler
{
    Task<KeysetPagedList<ProductDto>> Handle(GetProductsKeysetPaginationQuery query, CancellationToken cancellationToken);
}
