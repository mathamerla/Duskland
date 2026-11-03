using Duskland.BuildingBlocks.Infrastructure.Exceptions;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock.Command;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Inventory.Infrastructure.Context;
using Duskland.Modules.Products.Contracts.Features.GetProduct;
using EntityFramework.Exceptions.Common;

namespace Duskland.Modules.Inventory.Features.CreateStock;

internal class CreateStockHandler(InventoryDbContext Context, IGetProductHandler ProductHandler) : ICreateStockHandler
{
    public async Task<Guid> Handle(CreateStockCommand command, CancellationToken cancellationToken)
    {
        await ValidateProduct(command.ProductId, command.StoreId, cancellationToken);

        var stock = Stock.Create(command.StoreId, command.ProductId, command.Quantity);

        await Context.Stocks.AddAsync(stock, cancellationToken);
        await SaveStock(stock, cancellationToken);

        return stock.Id;
    }

    private async Task ValidateProduct(Guid productId, Guid storeId, CancellationToken cancellationToken)
    {
        var query = new GetProductQuery(productId, storeId);

        await ProductHandler.Handle(query, cancellationToken);
    }

    private async Task SaveStock(Stock stock, CancellationToken cancellationToken)
    {
        try
        {
            await Context.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException ex)
        {
            if (ex.ConstraintName == "IX_Stocks_StoreId_ProductId")
            {                
                throw new ConflictException($"Product {stock.ProductId} and store {stock.StoreId} is not unique for specified stock", ex);
            }

            throw;
        }
    }
}
