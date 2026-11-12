using System;
using Duskland.BuildingBlocks.Domain.Exceptions;
using Duskland.BuildingBlocks.Infrastructure.Exceptions;
using Duskland.Modules.Inventory.Contracts.Features.RemoveStock;
using Duskland.Modules.Inventory.Contracts.Features.RemoveStock.Command;
using Duskland.Modules.Inventory.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Inventory.Features.RemoveStock;

internal class RemoveStockHandler(InventoryDbContext Context) : IRemoveStockHandler
{
    public async Task Handle(RemoveStockCommand command, CancellationToken cancellationToken)
    {
        if (!IsValidStockToRemove(command.StockToRemove))
        {
            throw new DomainException("Stock to remove must be greater than zero.");
        }

        var result = await UpdateStockQuantity(command.StoreId, command.ProductId, command.StockToRemove, cancellationToken);

        if (result == 0)
        {
            if (await StockExists(command.StoreId, command.ProductId, cancellationToken))
            {
                throw new DomainException($"Removing {command.StockToRemove} from stock for product {command.ProductId} in store {command.StoreId} would result in a negative quantity.");
            }

            throw new NotFoundException($"Stock for product {command.ProductId} in store {command.StoreId} does not exist.");
        }
    }

    private static bool IsValidStockToRemove(int stockToRemove)
    {
        return stockToRemove > 0;
    }

    private Task<int> UpdateStockQuantity(Guid storeId, Guid productId, int stockToRemove, CancellationToken cancellationToken)
    {
        return Context.Stocks
            .Where(c => c.ProductId == productId && c.StoreId == storeId)
            .Where(c => c.Quantity - c.ReservedQuantity >= stockToRemove)
            .ExecuteUpdateAsync(c => c
                .SetProperty(c => c.Quantity, c => c.Quantity - stockToRemove)
                .SetProperty(c => c.Version, c => Guid.NewGuid()), cancellationToken);
    }

    private Task<bool> StockExists(Guid storeId, Guid productId, CancellationToken cancellationToken)
    {
        return Context.Stocks
            .AnyAsync(c => c.ProductId == productId && c.StoreId == storeId, cancellationToken);
    }
}
