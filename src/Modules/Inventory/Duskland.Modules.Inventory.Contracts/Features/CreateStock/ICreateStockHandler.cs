using System;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock.Command;

namespace Duskland.Modules.Inventory.Contracts.Features.CreateStock;

public interface ICreateStockHandler
{
    Task<Guid> Handle(CreateStockCommand command, CancellationToken cancellationToken);
}
