using System;
using Duskland.Modules.Products.Domain.Common;
using Duskland.Modules.Products.Features.GetProductsKeyset.Cursor;
using Duskland.Modules.Products.Helpers;

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

        if (!IsValidBase64Url(cursor))
        {
            throw new DomainException("Cursor is not a valid Base64 URL-encoded string.");
        }
    }

    internal static bool IsValidBase64Url(string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        try
        {
            var parsedCursor = CursorHelper<ProductPriceIdCursor>.Deserialize(cursor);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
