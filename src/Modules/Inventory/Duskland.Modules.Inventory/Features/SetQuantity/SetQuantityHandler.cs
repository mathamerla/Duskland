using System;
using Duskland.BuildingBlocks.Infrastructure.Exceptions;
using Duskland.Modules.Inventory.Contracts.Features.SetQuantity;
using Duskland.Modules.Inventory.Contracts.Features.SetQuantity.Command;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Inventory.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Inventory.Features.SetQuantity;

internal class SetQuantityHandler(InventoryDbContext Context): ISetQuantityHandler
{
    public async Task Handle(SetQuantityCommand command, CancellationToken cancellationToken)
    {
        var stock = await Stock(command.ProductId, command.StoreId, command.Version, cancellationToken);

        stock.SetQuantity(command.Quantity);

        await SaveChanges(cancellationToken);
    }

    private async Task<Stock> Stock(Guid productId, Guid storeId, Guid version, CancellationToken token)
    {
        var stock = await Context.Stocks
            .FirstOrDefaultAsync(c => c.ProductId == productId && c.StoreId == storeId, token)
                ?? throw new NotFoundException($"Stock with Product Id {productId} not found");
        
        if (stock.Version != version)
        {
            throw new PreConditionFailedException("Version of the stock is different than defined in request");
        }

        return stock;
    }

    private async Task SaveChanges(CancellationToken token)
    {
        try
        {
            await Context.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PreConditionFailedException("Version of the stock is different than defined in request");
        }
    } 
}
