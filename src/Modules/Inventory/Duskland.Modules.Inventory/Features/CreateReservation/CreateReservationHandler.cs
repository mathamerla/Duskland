using System;
using Duskland.BuildingBlocks.Domain.Exceptions;
using Duskland.BuildingBlocks.Infrastructure.Exceptions;
using Duskland.Modules.Inventory.Contracts.Features.CreateReservation;
using Duskland.Modules.Inventory.Contracts.Features.CreateReservation.Command;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock.Dtos;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Inventory.Domain.Enums;
using Duskland.Modules.Inventory.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Inventory.Features.CreateReservation;

internal class CreateReservationHandler(InventoryDbContext Context) : ICreateReservationHandler
{
    public async Task<CreateReservationDto> Handle(CreateReservationCommand command, CancellationToken cancellationToken)
    {
        var existingReservation = await ExistingReservation(command.StoreId, command.ProductId, command.OrderId, cancellationToken);

        if (existingReservation is not null)
        {
            return ProcessExistingReservation(existingReservation, command.Quantity);
        }

        await Context.Database.BeginTransactionAsync(cancellationToken);
        
        var updatedStocks = await UpdateStock(command.Quantity, command.ProductId, command.StoreId, cancellationToken);

        if (updatedStocks == 0)
        {
            await Context.Database.RollbackTransactionAsync(cancellationToken);

            throw new DomainException("Not enough stock available to make reservation");
        }

        var stock = await Stock(command.ProductId, command.StoreId, cancellationToken); 

        if (stock is null)
        {
            await Context.Database.RollbackTransactionAsync(cancellationToken);

            throw new NotFoundException($"Stock for product {command.ProductId} and store {command.StoreId} not found");        
        } 

        var expirationDate = ExpirationDate();

        var reservation = StockReservation.Create(
            command.ProductId, 
            command.StoreId, 
            command.OrderId, 
            stock, 
            command.Quantity, 
            expirationDate
        );

        await Context.StockReservations.AddAsync(reservation, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        await Context.Database.CommitTransactionAsync(cancellationToken);

        return new CreateReservationDto(reservation.Id, true);
    }

    private Task<StockReservation?> ExistingReservation(Guid storeId, Guid productId, Guid orderId, CancellationToken cancellationToken)
    {
        return Context.StockReservations
            .FirstOrDefaultAsync(r => 
                r.StoreId == storeId && 
                r.ProductId == productId && 
                r.OrderId == orderId &&
                (r.Status == ReservationStatus.ACTIVE || r.Status == ReservationStatus.CONFIRMED), 
            cancellationToken);
    }

    private static CreateReservationDto ProcessExistingReservation(StockReservation stockReservation, int quantity)
    {
        if (stockReservation.Quantity == quantity)
        {
            return new CreateReservationDto(stockReservation.Id, false);
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

    private async Task<Stock?> Stock(Guid productId, Guid storeId, CancellationToken cancellationToken)
    {
        return await Context.Stocks.FirstOrDefaultAsync(c => c.ProductId == productId && c.StoreId == storeId, cancellationToken);
    }

    private static DateTime ExpirationDate()
    {
        return DateTime.UtcNow.AddHours(1);
    }
}
