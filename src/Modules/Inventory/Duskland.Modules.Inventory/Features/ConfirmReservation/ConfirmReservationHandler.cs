using Duskland.BuildingBlocks.Domain.Exceptions;
using Duskland.BuildingBlocks.Infrastructure.Exceptions;
using Duskland.Modules.Inventory.Contracts.Features.ConfirmReservation;
using Duskland.Modules.Inventory.Contracts.Features.ConfirmReservation.Command;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Inventory.Domain.Enums;
using Duskland.Modules.Inventory.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Inventory.Features.ConfirmReservation;

internal class ConfirmReservationHandler(InventoryDbContext context) : IConfirmReservationHandler
{
    public async Task Handle(ConfirmReservationCommand command, CancellationToken cancellationToken)
    {
        var reservation = await Reservation(command, cancellationToken);

        if (IsConfirmedOrThrow(reservation))
        {
            return;
        }

        var transactionStatus = await RunTransaction(command, reservation, cancellationToken);
        
        if (transactionStatus == ReservationTransactionStatus.SUCCESS)
        {
            return;
        }

        if (transactionStatus == ReservationTransactionStatus.RESERVATION_NOT_UPDATED)
        {
            reservation = await Reservation(command, cancellationToken);

            if (IsConfirmedOrThrow(reservation))
            {
                return;
            }
        }

        if (transactionStatus == ReservationTransactionStatus.STOCK_NOT_UPDATED)
        {
            reservation = await Reservation(command, cancellationToken);

            if (IsConfirmedOrThrow(reservation))
            {
                return;
            }

            var stock = await context.Stocks.AsNoTracking()
                .FirstOrDefaultAsync(stock =>
                    stock.Id == reservation.StockId &&
                    stock.StoreId == command.StoreId &&
                    stock.ProductId == command.ProductId, cancellationToken)
                ?? throw new NotFoundException($"Stock for store {command.StoreId} and product {command.ProductId} does not exist");

            if (stock.Quantity < reservation.Quantity || stock.ReservedQuantity < reservation.Quantity || stock.Quantity < stock.ReservedQuantity)
            {
                throw new DomainException($"Stock for product {command.ProductId} and store {command.StoreId} does not have enough quantity or reservation to process request");
            }
        }

        throw new InvalidOperationException("Operation failed due to unknown reasons");
    }

    private async Task<ReservationTransactionStatus> RunTransaction(ConfirmReservationCommand command, StockReservation reservation, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        int updatedReservations = await UpdateReservation(command, cancellationToken);

        if (updatedReservations == 0)
        {
            return ReservationTransactionStatus.RESERVATION_NOT_UPDATED;
        }

        int updatedStocks = await UpdateStock(command, reservation, cancellationToken);

        if (updatedStocks == 0)
        {
            return ReservationTransactionStatus.STOCK_NOT_UPDATED;
        }

        await transaction.CommitAsync(cancellationToken);
        
        return ReservationTransactionStatus.SUCCESS;
    }

    private Task<int> UpdateStock(ConfirmReservationCommand command, StockReservation reservation, CancellationToken cancellationToken)
    {
        return context.Stocks
            .Where(stock => 
                stock.Id == reservation.StockId && 
                stock.StoreId == command.StoreId && 
                stock.ProductId == command.ProductId)
            .Where(stock => 
                stock.Quantity - reservation.Quantity >= 0 && 
                stock.ReservedQuantity - reservation.Quantity >= 0)
            .Where(stock => stock.Quantity >= stock.ReservedQuantity)
            .ExecuteUpdateAsync(update => update
                .SetProperty(stock => stock.Quantity, stock => stock.Quantity - reservation.Quantity)
                .SetProperty(stock => stock.ReservedQuantity, stock => stock.ReservedQuantity - reservation.Quantity)
                .SetProperty(stock => stock.Version, stock => Guid.NewGuid()), cancellationToken);
    }

    private Task<int> UpdateReservation(ConfirmReservationCommand command, CancellationToken cancellationToken)
    {
        return context.StockReservations
            .Where(r => 
                r.Id == command.ReservationId && 
                r.StoreId == command.StoreId && 
                r.ProductId == command.ProductId)
            .Where(r => 
                r.Status == ReservationStatus.ACTIVE && 
                r.ExpirationDate > InventoryDbContext.ClockTimestamp())
            .ExecuteUpdateAsync(update => update.SetProperty(r => r.Status, ReservationStatus.CONFIRMED), cancellationToken);
    }

    private async Task<StockReservation> Reservation(ConfirmReservationCommand command, CancellationToken cancellationToken)
        => await context.StockReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => 
                r.Id == command.ReservationId && 
                r.StoreId == command.StoreId && 
                r.ProductId == command.ProductId,
                cancellationToken)
            ?? throw new NotFoundException($"Reservation with ID {command.ReservationId} does not exist");

    private static bool IsConfirmedOrThrow(StockReservation reservation)
    {
        if (reservation.Status == ReservationStatus.CONFIRMED)
        {
            return true;
        }

        if (reservation.IsActiveAndEffective())
        {
            return false;
        }

        throw new ConflictException($"Reservation {reservation.Id} is expired or already released.");
    }
}
