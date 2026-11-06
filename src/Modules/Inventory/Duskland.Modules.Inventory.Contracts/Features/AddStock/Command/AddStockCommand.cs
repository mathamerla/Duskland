namespace Duskland.Modules.Inventory.Contracts.Features.AddStock.Command;

public record class AddStockCommand(Guid StoreId, Guid ProductId, int StockToAdd);
