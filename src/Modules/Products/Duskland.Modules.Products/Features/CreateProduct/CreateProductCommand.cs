using Duskland.Modules.Products.Domain;
using Duskland.Modules.Products.Infrastructure.DAL.Context;
using Duskland.Modules.Products.Infrastructure.DAL.Exceptions;
using EntityFramework.Exceptions.Common;

namespace Duskland.Modules.Products.Features.CreateProduct;

public record CreateProductCommand(Guid StoreId, string SKU, string Name, decimal Price);

public interface ICreateProductHandler
{
    Task<Guid> Handle(CreateProductCommand command, CancellationToken cancellationToken);
}

internal class CreateProductHandler(ProductDbContext Context): ICreateProductHandler
{
    public async Task<Guid> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var product = Product.Create(command.StoreId, command.SKU, command.Name, command.Price);

        await Context.Products.AddAsync(product, cancellationToken);
        await SaveProduct(product, cancellationToken);

        return product.Id;
    }

    private async Task SaveProduct(Product product, CancellationToken cancellationToken)
    {
        try
        {
            await Context.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException ex)
        {
            if (ex.ConstraintName == "IX_Products_StoreId_SKU")
            {                
                throw new ConflictException($"SKU {product.SKU} is not unique for specified store", ex);
            }

            throw;
        }
    }

}