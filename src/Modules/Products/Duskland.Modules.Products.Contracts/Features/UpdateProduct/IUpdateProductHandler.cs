using System;
using Duskland.Modules.Products.Contracts.Features.UpdateProduct.Command;

namespace Duskland.Modules.Products.Contracts.Features.UpdateProduct;

public interface IUpdateProductHandler
{
    Task Handle(UpdateProductCommand command, CancellationToken token);
}
