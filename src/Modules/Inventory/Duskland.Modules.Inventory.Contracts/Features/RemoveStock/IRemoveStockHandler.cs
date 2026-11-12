using System;
using Duskland.Modules.Inventory.Contracts.Features.RemoveStock.Command;

namespace Duskland.Modules.Inventory.Contracts.Features.RemoveStock;

public interface IRemoveStockHandler
{
    Task Handle(RemoveStockCommand command, CancellationToken cancellationToken);
}
