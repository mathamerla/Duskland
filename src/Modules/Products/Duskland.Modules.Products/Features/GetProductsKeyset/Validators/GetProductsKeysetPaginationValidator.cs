using System;
using Duskland.Modules.Products.Domain.Common;

namespace Duskland.Modules.Products.Features.GetProductsKeyset.Validators;

internal static class GetProductsKeysetPaginationValidator
{
    internal static void Validate(int pageSize, string cursor)
    {
        if (pageSize <= 0)
        {
            throw new DomainException("Page size must be greater than zero.");
        }

        if (pageSize > 100)
        {
            throw new DomainException("Page size must not exceed 100.");
        }
    }
}
