using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;
using Duskland.Modules.Products.Contracts.Features.GetProductsKeysetPagination.Dtos;
using Duskland.Modules.Products.Contracts.Features.GetProductsKeysetPagination.Queries;
using Duskland.Modules.Products.Domain.Common;
using Duskland.Modules.Products.Features.GetProductsKeyset.Cursor;
using Duskland.Modules.Products.Features.GetProductsKeyset.Validators;
using Duskland.Modules.Products.Helpers;
using Duskland.Modules.Products.Infrastructure.DAL.Context;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Products.Features.GetProductsKeyset;

internal class GetProductsKeysetPaginationHandler(ProductDbContext Context) : IGetProductsKeysetPaginationHandler
{
    public async Task<KeysetPagedList<ProductDto>> Handle(GetProductsKeysetPaginationQuery query, CancellationToken cancellationToken)
    {
        GetProductsKeysetPaginationValidator.Validate(query.PageSize, query.Cursor);
        
        var cursorData = GetParsedCursor(query.Cursor);

        var pageSize = query.PageSize + 1;

        var products = await GetProductsAsync(query.StoreId, cursorData, pageSize, cancellationToken);

        var nextCursor = GetNextCursor(products, query.PageSize);

        var items = products.Take(query.PageSize).ToList();

        return new KeysetPagedList<ProductDto>(query.PageSize, nextCursor, items);
    }

    private static ProductPriceIdCursor? GetParsedCursor(string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null; 
        }

        return CursorHelper<ProductPriceIdCursor>.Deserialize(cursor) 
            ?? throw new DomainException("Invalid cursor format.");
    }

    private async Task<List<ProductDto>> GetProductsAsync(Guid storeId, ProductPriceIdCursor? cursorData, int pageSize, CancellationToken cancellationToken)
    {
        var query = Context.Products
            .Where(p => p.StoreId == storeId);

        if (cursorData != null)
        {
            query = query.Where(p => p.Price > cursorData.Price || (p.Price == cursorData.Price && p.Id > cursorData.Id));
        }

        var products = await query
            .AsNoTracking()
            .OrderBy(p => p.Price)
                .ThenBy(p => p.Id)
            .Take(pageSize)
            .Select(p => new ProductDto(p.Id, p.StoreId, p.SKU, p.Name, p.Price))
            .ToListAsync(cancellationToken);

        return products;
    }

    private static string GetNextCursor(IReadOnlyList<ProductDto> products, int pageSize)
    {
        if (products.Count <= pageSize)
        {
            return string.Empty; 
        }

        var lastProduct = products[pageSize - 1];

        var cursorInput = new ProductPriceIdCursor(lastProduct.Price, lastProduct.Id);

        return CursorHelper<ProductPriceIdCursor>.Serialize(cursorInput);
    }
}
