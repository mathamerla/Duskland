namespace Duskland.Modules.Inventory.Contracts.Features.SetQuantity.Command;

public record class SetQuantityCommand(Guid StoreId, Guid ProductId, int Quantity, Guid Version);
