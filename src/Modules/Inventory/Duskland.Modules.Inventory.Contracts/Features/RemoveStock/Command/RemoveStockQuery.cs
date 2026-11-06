namespace Duskland.Modules.Inventory.Contracts.Features.RemoveStock.Command;

public record class RemoveStockQuery(Guid StoreId, Guid ProductId, int StockToRemove);
