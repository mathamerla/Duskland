using Duskland.BuildingBlocks.Application.Responses;
using System;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;

namespace Duskland.Modules.Products.Contracts.Features.GetProductsKeysetPagination.Queries;

public interface IGetProductsKeysetPaginationHandler
{
    Task<KeysetPagedList<ProductDto>> Handle(GetProductsKeysetPaginationQuery query, CancellationToken cancellationToken);
}
