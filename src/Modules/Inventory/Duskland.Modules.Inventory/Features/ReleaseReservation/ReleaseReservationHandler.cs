using System;
using Duskland.BuildingBlocks.Domain.Exceptions;
using Duskland.BuildingBlocks.Infrastructure.Exceptions;
using Duskland.Modules.Inventory.Contracts.Features.ReleaseReservation;
using Duskland.Modules.Inventory.Contracts.Features.ReleaseReservation.Command;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Inventory.Domain.Enums;
using Duskland.Modules.Inventory.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Inventory.Features.ReleaseReservation;


internal class ReleaseReservationHandler(InventoryDbContext Context) : IReleaseReservationHandler
{
    public async Task Handle(ReleaseReservationCommand command, CancellationToken cancellationToken)
    {
        var reservation = await Reservation(command, cancellationToken);

        if (IsReservationProcessed(reservation))
        {
            return;
        }

        var transactionStatus = await RunTransaction(command, reservation.Quantity, cancellationToken);

        if (transactionStatus == ReservationTransactionStatus.SUCCESS)
        {
            return;
        }

        if (transactionStatus == ReservationTransactionStatus.RESERVATION_NOT_UPDATED)
        {
            var isProcessed = await ValidateNotUpdateReservationResult(command, cancellationToken); 

            if (isProcessed)
            {
                return;
            }
        }

        if (transactionStatus == ReservationTransactionStatus.STOCK_NOT_UPDATED)
        {
            var isProcessed = await ValidateInvalidStockResult(command, cancellationToken);

            if (isProcessed)
            {
                return;
            }
        }

        throw new InvalidOperationException("Operation failed due to unknown reasons");
    }

    private async Task<StockReservation> Reservation(ReleaseReservationCommand command, CancellationToken cancellationToken)
    {
        return await Context.StockReservations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => 
                c.Id == command.ReservationId && 
                c.StoreId == command.StoreId && 
                c.ProductId == command.ProductId, cancellationToken)
                    ?? throw new NotFoundException($"Reservation with ID {command.ReservationId} does not exist");
    }

    private async Task<ReservationTransactionStatus> RunTransaction(ReleaseReservationCommand command, int quantity, CancellationToken cancellationToken)
    {
        using var transaction = await Context.Database.BeginTransactionAsync(cancellationToken);

        var updatedReservations = await ReleaseReservation(command.ReservationId, cancellationToken);

        if (updatedReservations == 0)
        {
            return ReservationTransactionStatus.RESERVATION_NOT_UPDATED;
        }

        var updatedStocks = await ReleaseStock(command.StoreId, command.ProductId, quantity, cancellationToken);

        if (updatedStocks == 0)
        {
            return ReservationTransactionStatus.STOCK_NOT_UPDATED;
        }

        await transaction.CommitAsync(cancellationToken);

        return ReservationTransactionStatus.SUCCESS;
    }

    private async Task<bool> ValidateInvalidStockResult(ReleaseReservationCommand command, CancellationToken cancellationToken)
    {
        var reservation = await Reservation(command, cancellationToken);     

        if (IsReservationProcessed(reservation))
        {
            return true;
        }

        var stock = await Stock(command.StoreId, command.ProductId, cancellationToken);

        if (stock.ReservedQuantity < reservation.Quantity)
        {
            throw new DomainException($"Stock for product {command.ProductId} and store {command.StoreId} does not have enough reservation to process request");
        }

        return false;
    }

    private async Task<Stock> Stock(Guid storeId, Guid productId, CancellationToken cancellationToken)
    {
        return await Context.Stocks
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.StoreId == storeId && c.ProductId == productId, cancellationToken)
                ?? throw new NotFoundException($"Stock for store {storeId} and product {productId}does not exist");
    }

    private Task<int> ReleaseStock(Guid storeId, Guid productId, int quantity, CancellationToken cancellationToken)
    {
        return Context.Stocks
            .Where(c => c.StoreId == storeId && c.ProductId == productId)
            .Where(c => c.ReservedQuantity >= quantity)
            .ExecuteUpdateAsync(c => c
                .SetProperty(c => c.ReservedQuantity, c => c.ReservedQuantity - quantity)
                .SetProperty(c => c.Version, c => Guid.NewGuid()), cancellationToken);     
    }

    private Task<int> ReleaseReservation(Guid reservationId, CancellationToken cancellationToken)
    {
        return Context.StockReservations
            .Where(c => c.Id == reservationId)
            .Where(c => c.Status == ReservationStatus.ACTIVE)
            .ExecuteUpdateAsync(c => c
                .SetProperty(c => c.Status, ReservationStatus.RELEASED), cancellationToken); 
    }

    private static bool IsReservationProcessed(StockReservation reservation)
    {
        if (reservation.IsExpired() || reservation.IsReleased())
        {
            return true;
        }

        if (reservation.IsActive())
        {
            return false;
        }

        throw new ConflictException($"Reservation {reservation.Id} already confirmed.");
    }

    private async Task<bool> ValidateNotUpdateReservationResult(ReleaseReservationCommand command, CancellationToken cancellationToken)
    {
        var reservation = await Reservation(command, cancellationToken);

        if (IsReservationProcessed(reservation))
        {
            return true;
        }

        return false;
    }
}
