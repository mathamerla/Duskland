namespace Duskland.Api.Modules.Inventory.Requests;

public record class CreateReservationRequest(Guid OrderId, int Quantity);
