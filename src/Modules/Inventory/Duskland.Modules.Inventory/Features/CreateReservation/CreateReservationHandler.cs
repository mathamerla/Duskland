using Duskland.BuildingBlocks.Domain.Exceptions;
using Duskland.BuildingBlocks.Infrastructure.Exceptions;
using Duskland.Modules.Inventory.Contracts.Features.CreateReservation;
using Duskland.Modules.Inventory.Contracts.Features.CreateReservation.Command;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock.Dtos;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Inventory.Domain.Enums;
using Duskland.Modules.Inventory.Infrastructure.Context;
using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Inventory.Features.CreateReservation;

internal class CreateReservationHandler(InventoryDbContext Context) : ICreateReservationHandler
{
    public async Task<CreateReservationDto> Handle(CreateReservationCommand command, CancellationToken cancellationToken)
    {
        var reservationId = await ExistingReservationId(command, cancellationToken);

        if (reservationId is not null)
        {
            return new CreateReservationDto(reservationId.Value, false);
        }

        var stock = await Stock(command.ProductId, command.StoreId, cancellationToken); 

        var expirationDate = ExpirationDate();

        var reservation = StockReservation.Create(
            command.ProductId, 
            command.StoreId, 
            command.OrderId, 
            stock, 
            command.Quantity, 
            expirationDate
        );

        using var transaction = await Context.Database.BeginTransactionAsync(cancellationToken);
        
        var updatedStocks = await UpdateStock(command.Quantity, command.ProductId, command.StoreId, cancellationToken);

        if (updatedStocks == 0)
        {
            var existingReservationId = await ExistingReservationId(command, cancellationToken);

            if (existingReservationId is not null)
            {
                return new CreateReservationDto(existingReservationId.Value, false);
            }

            throw new DomainException("Not enough stock available to make reservation");
        }

        await Context.StockReservations.AddAsync(reservation, cancellationToken);
        await SaveChanges(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new CreateReservationDto(reservation.Id, true);
    }

    private Task<StockReservation?> ExistingReservation(Guid storeId, Guid productId, Guid orderId, CancellationToken cancellationToken)
    {
        return Context.StockReservations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => 
                r.StoreId == storeId && 
                r.ProductId == productId && 
                r.OrderId == orderId,
            cancellationToken);
    }

    private async Task<Guid?> ExistingReservationId(CreateReservationCommand command, CancellationToken cancellationToken)
    {
        var existingReservation = await ExistingReservation(command.StoreId, command.ProductId, command.OrderId, cancellationToken);

        if (existingReservation is null)
        {
            return null;
        }

        if (existingReservation.Quantity == command.Quantity && existingReservation.IsActive())
        {
            return existingReservation.Id;
        }

        throw new ConflictException("Transaction for given product and order already exists");
    } 

    private Task<int> UpdateStock(int quantity, Guid productId, Guid storeId, CancellationToken cancellationToken)
    {
        return Context.Stocks
            .Where(c => c.ProductId == productId && c.StoreId == storeId)
            .Where(c => c.Quantity - c.ReservedQuantity >= quantity)
            .ExecuteUpdateAsync(c => c
                .SetProperty(c => c.ReservedQuantity, c => c.ReservedQuantity + quantity)
                .SetProperty(c => c.Version, c => Guid.NewGuid()), cancellationToken);    
    }

    private async Task<Stock> Stock(Guid productId, Guid storeId, CancellationToken cancellationToken)
    {
        return await Context.Stocks.FirstOrDefaultAsync(c => c.ProductId == productId && c.StoreId == storeId, cancellationToken) ??
            throw new NotFoundException($"Stock for product {productId} and store {storeId} not found");        
    }

    private async Task SaveChanges(CancellationToken cancellationToken)
    {
        try
        {
            await Context.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException ex)
        {
            if (ex.ConstraintName == "IX_StockReservations_StoreId_ProductId_OrderId")
            {                
                throw new ConflictException($"Reservation for this order id and product id already exists", ex);
            }

            throw;
        }
    }

    private static DateTime ExpirationDate()
    {
        return DateTime.UtcNow.AddHours(1);
    }
}
