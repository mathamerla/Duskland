using System;
using Duskland.BuildingBlocks.Application.Templates;
using Duskland.BuildingBlocks.Infrastructure.Exceptions;
using Duskland.Modules.Inventory.Contracts.Features.GetStock;
using Duskland.Modules.Inventory.Contracts.Features.GetStock.Dtos;
using Duskland.Modules.Inventory.Contracts.Features.GetStock.Queries;
using Duskland.Modules.Inventory.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Inventory.Features.GetStock;

internal class GetStockHandler(InventoryDbContext Context) : IGetStockHandler
{
    public async Task<VersionedDto<StockDto>> Handle(GetStockQuery query, CancellationToken token)
    {
        return await Context.Stocks
            .AsNoTracking()
            .Where(c => c.ProductId == query.ProductId && c.StoreId == query.StoreId)
            .Select(c => new VersionedDto<StockDto>(c.Version, new StockDto(c.Id, c.StoreId, c.ProductId, c.Quantity, c.ReservedQuantity, c.AvailableQuantity)))
            .FirstOrDefaultAsync(token)
                ?? throw new NotFoundException($"Stock for product {query.ProductId} and store {query.StoreId} not found");
    }
}