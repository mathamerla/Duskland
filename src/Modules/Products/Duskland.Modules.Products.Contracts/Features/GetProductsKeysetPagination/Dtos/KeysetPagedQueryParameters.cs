using System;

namespace Duskland.Modules.Products.Contracts.Features.GetProductsKeysetPagination.Dtos;

public record KeysetPagedQueryParameters(int PageSize, string Cursor);
