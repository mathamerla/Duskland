using System;
using Duskland.Modules.Inventory.Contracts.Features.AddStock.Command;

namespace Duskland.Modules.Inventory.Contracts.Features.AddStock;

public interface IAddStockHandler
{
    Task Handle(AddStockCommand command, CancellationToken cancellationToken);
}
