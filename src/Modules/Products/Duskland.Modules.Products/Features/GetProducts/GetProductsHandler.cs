using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Queries;
using Duskland.Modules.Products.Domain;
using Duskland.Modules.Products.Features.GetProducts.Validators;
using Duskland.Modules.Products.Infrastructure.DAL.Context;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Products.Features.GetProducts;

internal class GetProductsHandler(ProductDbContext Context) : IGetProductsHandler
{
    public async Task<PagedList<ProductDto>> Handle(GetProductsQuery query, CancellationToken token)
    {
        GetProductsValidator.Validate(query);

        var products = Products(query.StoreId);

        if (query.Parameters.HasSorting)
        {
            products = SortedProducts(products, query.Parameters.SortBy!, query.Parameters.Direction);
        }

        if (query.HasSearch)
        {
            products = FilterByName(products, query.SearchByName!);
            
        }
        
        var total = await products.CountAsync(token);

        var pagedList = OffsetList(products, query.Parameters.SkipCount(), query.Parameters.PageSize);

        var result = await MapToProductDto(pagedList, token);

        return new PagedList<ProductDto>(query.Parameters.Page, query.Parameters.PageSize, total, result);
    }

    private IQueryable<Product> Products(Guid storeId)
    {
        return Context.Products
            .AsNoTracking()
            .Where(c => c.StoreId == storeId);
    }

    private static IQueryable<Product> SortedProducts(IQueryable<Product> products, string sortBy, Direction direction)
    {
        IOrderedQueryable<Product>? result = null;

        if (direction == Direction.ASC)
        {
            result = sortBy switch
            {
                "name" => products.OrderBy(p => p.Name),
                "price" => products.OrderBy(p => p.Price),
                "sku" => products.OrderBy(p => p.SKU),
                _ => throw new ArgumentException("Invalid sort field")
            };   
        }
        else
        {
            result = sortBy switch
            {
                "name" => products.OrderByDescending(p => p.Name),
                "price" => products.OrderByDescending(p => p.Price),
                "sku" => products.OrderByDescending(p => p.SKU),
                _ => throw new ArgumentException("Invalid sort field")
            };
        }

        return result.ThenBy(c => c.Id);
    }

    private static IQueryable<Product> FilterByName(IQueryable<Product> products, string searchBy)
    {
        return products.Where(c => EF.Functions.ILike(c.Name, searchBy));
    }

    private static IQueryable<Product> OffsetList(IQueryable<Product> products, int skip, int pageSize)
    {
        return products
            .Skip(skip)
            .Take(pageSize);
    }

    private static Task<List<ProductDto>> MapToProductDto(IQueryable<Product> products, CancellationToken token)
    {
        return products
           .Select(c => new ProductDto(c.Id, c.StoreId, c.SKU, c.Name, c.Price))
           .ToListAsync(token);
    }
}
