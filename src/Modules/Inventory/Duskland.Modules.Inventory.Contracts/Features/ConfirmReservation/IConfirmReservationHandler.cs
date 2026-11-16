using Duskland.Modules.Inventory.Contracts.Features.ConfirmReservation.Command;

namespace Duskland.Modules.Inventory.Contracts.Features.ConfirmReservation;

public interface IConfirmReservationHandler
{
    Task Handle(ConfirmReservationCommand command, CancellationToken cancellationToken);
}
