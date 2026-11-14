namespace Duskland.Modules.Inventory.Contracts.Features.CreateReservation.Command;

public record class CreateReservationCommand(Guid StoreId, Guid ProductId, Guid OrderId, int Quantity);
