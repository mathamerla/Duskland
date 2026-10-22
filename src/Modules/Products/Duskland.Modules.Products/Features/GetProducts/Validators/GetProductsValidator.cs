using System;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Queries;
using Duskland.Modules.Products.Domain.Common;

namespace Duskland.Modules.Products.Features.GetProducts.Validators;

public static class GetProductsValidator
{
    private static readonly List<string> ALLOWED_SORT = ["price", "sku", "name"];

    public static void Validate(GetProductsQuery query)
    {
        if (query.StoreId == Guid.Empty)
        {
           throw new DomainException("StoreId can't be empty"); 
        }

        if (query.Parameters.HasSorting && !ALLOWED_SORT.Contains(query.Parameters.SortBy!))
        {
           throw new DomainException("Wrong sorting defined"); 
        }

        if (query.Parameters.PageSize < 1 || query.Parameters.PageSize > 100)
        {
           throw new DomainException("The page size needs to be between 1 and 100"); 
        }

        if (query.Parameters.Page < 1)
        {
            throw new DomainException("The page can't be lower than 1");
        }
    }
}
