using System;

namespace Duskland.Modules.Products.Contracts.Features.GetProductsKeysetPagination.Dtos;

public record KeysetPagedList<T>(int PageSize, string NextCursor, IEnumerable<T> Items)
{
    public bool HasMore => !string.IsNullOrEmpty(NextCursor);
}
