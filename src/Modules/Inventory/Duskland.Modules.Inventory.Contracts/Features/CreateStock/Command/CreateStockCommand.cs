namespace Duskland.Modules.Inventory.Contracts.Features.CreateStock.Command;

public record class CreateStockCommand(Guid StoreId, Guid ProductId, int Quantity);
