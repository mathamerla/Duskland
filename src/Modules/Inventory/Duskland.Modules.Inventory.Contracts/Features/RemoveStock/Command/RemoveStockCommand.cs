namespace Duskland.Modules.Inventory.Contracts.Features.RemoveStock.Command;

public record class RemoveStockCommand(Guid StoreId, Guid ProductId, int StockToRemove);
