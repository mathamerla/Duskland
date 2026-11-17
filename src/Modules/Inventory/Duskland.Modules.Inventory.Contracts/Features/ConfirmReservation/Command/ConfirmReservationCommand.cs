namespace Duskland.Modules.Inventory.Contracts.Features.ConfirmReservation.Command;

public record class ConfirmReservationCommand(Guid StoreId, Guid ProductId, Guid ReservationId);
