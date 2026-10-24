using Duskland.Modules.Products.Contracts.Features.GetProduct;
using Duskland.Modules.Products.Contracts.Features.GetProduct.Dtos;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;
using Duskland.Modules.Products.Infrastructure.DAL.Context;
using Duskland.Modules.Products.Infrastructure.DAL.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Products.Features.GetProduct;

internal class GetProductHandler(ProductDbContext Context) : IGetProductHandler
{
    public async Task<ProductDtoWithVersion> Handle(GetProductQuery query, CancellationToken token)
    {
        return await Context.Products
            .AsNoTracking()
            .Where(c => c.Id == query.Id)
            .Select(c => new ProductDtoWithVersion(new ProductDto(c.Id, c.StoreId, c.SKU, c.Name, c.Price), c.Version))
            .FirstOrDefaultAsync(token)
                ?? throw new NotFoundException($"Product with Id {query.Id} not found");
    }
}
