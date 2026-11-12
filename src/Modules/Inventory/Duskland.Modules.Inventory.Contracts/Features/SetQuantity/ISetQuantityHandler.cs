using System;
using Duskland.Modules.Inventory.Contracts.Features.SetQuantity.Command;

namespace Duskland.Modules.Inventory.Contracts.Features.SetQuantity;

public interface ISetQuantityHandler
{
    public Task Handle(SetQuantityCommand command, CancellationToken cancellationToken);
}
