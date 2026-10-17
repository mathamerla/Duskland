using Duskland.Modules.Products.Contracts.Features.CreateProduct.Command;

namespace Duskland.Modules.Products.Contracts.Features.CreateProduct;

public interface ICreateProductHandler
{
    Task<Guid> Handle(CreateProductCommand command, CancellationToken cancellationToken);
}