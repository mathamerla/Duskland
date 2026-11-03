namespace Duskland.Modules.Inventory.Contracts.Features.GetStock.Dtos;

public record class StockDto (Guid Id, Guid StoreId, Guid ProductId, int Quantity, int ReservedQuantity, int AvailableQuantity);