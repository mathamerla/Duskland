using System;
using Duskland.Modules.Inventory.Contracts.Features.ReleaseReservation.Command;

namespace Duskland.Modules.Inventory.Contracts.Features.ReleaseReservation;

public interface IReleaseReservationHandler
{
    Task Handle(ReleaseReservationCommand command, CancellationToken cancellationToken);
}
