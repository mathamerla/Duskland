using System;

namespace Duskland.Modules.Inventory.Contracts.Features.GetStock.Queries;

public record GetStockQuery(Guid StoreId, Guid ProductId);
