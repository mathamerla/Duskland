using System;
using Duskland.BuildingBlocks.Domain.Exceptions;
using Duskland.BuildingBlocks.Infrastructure.Exceptions;
using Duskland.Modules.Inventory.Contracts.Features.AddStock;
using Duskland.Modules.Inventory.Contracts.Features.AddStock.Command;
using Duskland.Modules.Inventory.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Inventory.Features.AddStock;

internal class AddStockHandler(InventoryDbContext Context) : IAddStockHandler
{
    public async Task Handle(AddStockCommand command, CancellationToken cancellationToken)
    {
        if (!await StockExists(command.StoreId, command.ProductId, cancellationToken))
        {
            throw new NotFoundException($"Stock for product {command.ProductId} in store {command.StoreId} does not exist.");
        }

        if (!IsValidStockToAdd(command.StockToAdd))
        {
            throw new DomainException("Stock to add must be greater than zero.");
        }

        await UpdateStockQuantity(command.StoreId, command.ProductId, command.StockToAdd, cancellationToken);
    }

    private static bool IsValidStockToAdd(int stockToAdd)
    {
        return stockToAdd > 0;
    }

    private Task<bool> StockExists(Guid storeId, Guid productId, CancellationToken cancellationToken)
    {
        return Context.Stocks.AnyAsync(c => c.ProductId == productId && c.StoreId == storeId, cancellationToken);
    }

    private Task<int> UpdateStockQuantity(Guid storeId, Guid productId, int stockToAdd, CancellationToken cancellationToken)
    {
        return Context.Stocks
            .Where(c => c.ProductId == productId && c.StoreId == storeId)
            .ExecuteUpdateAsync(c => c
                .SetProperty(c => c.Quantity, c => c.Quantity + stockToAdd)
                .SetProperty(c => c.Version, c => Guid.NewGuid()), cancellationToken);
    }
}
