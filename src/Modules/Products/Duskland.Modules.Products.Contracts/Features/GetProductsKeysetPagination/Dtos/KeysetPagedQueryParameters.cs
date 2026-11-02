using System;

namespace Duskland.Modules.Products.Contracts.Features.GetProductsKeysetPagination.Dtos;

public record KeysetPagedQueryParameters(string Cursor = "", int PageSize = 20);
