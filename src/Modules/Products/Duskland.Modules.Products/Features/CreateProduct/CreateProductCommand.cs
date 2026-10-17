using System;
using Duskland.Modules.Products.Domain;
using Duskland.Modules.Products.Infrastructure.DAL.Context;

namespace Duskland.Modules.Products.Features.CreateProduct;

public record CreateProductCommand(Guid StoreId, string SKU, string Name, decimal Price);

/// <summary>
/// Temp solution - public Handler, later when MediatR like solution is implemented handler will be changed to internal
/// </summary>
public class CreateProductHandler(ProductDbContext Context)
{
    public async Task<Guid> Handle(CreateProductCommand command, CancellationToken token)
    {
        var product = Product.Create(command.StoreId, command.SKU, command.Name, command.Price);

        await Context.Products.AddAsync(product, token);
        await Context.SaveChangesAsync(token);

        return product.Id;
    }

}