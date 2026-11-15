namespace Duskland.Modules.Inventory.Contracts.Features.ReleaseReservation.Command;

public record class ReleaseReservationCommand(Guid StoreId, Guid ProductId, Guid ReservationId);
