using Duskland.Modules.Products.Contracts.Features.UpdateProduct;
using Duskland.Modules.Products.Contracts.Features.UpdateProduct.Command;
using Duskland.Modules.Products.Domain;
using Duskland.Modules.Products.Infrastructure.DAL.Context;
using Duskland.Modules.Products.Infrastructure.DAL.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Products.Features.UpdateProduct;

internal class UpdateProductHandler(ProductDbContext Context): IUpdateProductHandler
{
    public async Task Handle(UpdateProductCommand command, CancellationToken token)
    {
        var product = await Product(command.Id, command.Version, token);

        product.UpdateDetails(command.Name, command.Price);

        await UpdateProduct(token);
    }

    private async Task<Product> Product(Guid id,  Guid version, CancellationToken token)
    {
        var product = await Context.Products
            .FirstOrDefaultAsync(c => c.Id == id, token)
                ?? throw new NotFoundException($"Product with Id {id} not found");
        
        if (product.Version != version)
        {
            throw new PreConditionFailedException("Version of the product is different than defined in request");
        }

        return product;
    }

    private async Task UpdateProduct(CancellationToken token)
    {
        try
        {
            await Context.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PreConditionFailedException("Version of the product is different than defined in request");
        }
    } 
}
