using System;
using Duskland.BuildingBlocks.Application.Templates;
using Duskland.Modules.Inventory.Contracts.Features.GetStock.Dtos;
using Duskland.Modules.Inventory.Contracts.Features.GetStock.Queries;

namespace Duskland.Modules.Inventory.Contracts.Features.GetStock;

public interface IGetStockHandler
{
    Task<VersionedDto<StockDto>> Handle(GetStockQuery query, CancellationToken cancellationToken);
}
