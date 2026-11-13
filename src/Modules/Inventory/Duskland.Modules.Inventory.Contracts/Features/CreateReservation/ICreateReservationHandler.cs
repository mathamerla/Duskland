using System;
using Duskland.Modules.Inventory.Contracts.Features.CreateReservation.Command;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock.Dtos;

namespace Duskland.Modules.Inventory.Contracts.Features.CreateReservation;

public interface ICreateReservationHandler
{
    Task<CreateReservationDto> Handle(CreateReservationCommand command, CancellationToken cancellationToken);
}
