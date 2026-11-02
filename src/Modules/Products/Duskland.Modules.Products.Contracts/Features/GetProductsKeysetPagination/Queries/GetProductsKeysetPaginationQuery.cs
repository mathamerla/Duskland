using System;

namespace Duskland.Modules.Products.Contracts.Features.GetProductsKeysetPagination.Queries;

public record GetProductsKeysetPaginationQuery (Guid StoreId, int PageSize, string Cursor);
