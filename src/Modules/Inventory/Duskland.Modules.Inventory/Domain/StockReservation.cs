using System;
using Duskland.BuildingBlocks.Domain.Exceptions;
using Duskland.Modules.Inventory.Domain.Enums;

namespace Duskland.Modules.Inventory.Domain;

public class StockReservation
{
    public Guid Id { get; private set; }

    public Guid StockId { get; private set; }
    public Stock Stock { get; private set; } = null!;
    
    public Guid ProductId { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid OrderId { get; private set; }
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpirationDate { get; private set; }

    public static StockReservation Create(Guid productId, Guid storeId, Guid orderId, Stock stock, int quantity, DateTime expirationDate)
    {
        var reservation = new StockReservation()
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            StoreId = storeId,
            OrderId = orderId,
            StockId = stock.Id,
            Stock = stock,
            CreatedAt = DateTime.UtcNow,
            Status = ReservationStatus.ACTIVE
        };

        reservation.SetQuantity(quantity);
        reservation.SetExpirationDate(expirationDate);

        return reservation;
    }

    public void SetQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero");
        }

        Quantity = quantity;
    }

    public void SetExpirationDate(DateTime expirationDate)
    {
        if (expirationDate < CreatedAt)
        {
            throw new DomainException("Expiration date must be in the future");
        }

        ExpirationDate = expirationDate;
    }

    public bool IsActive()
    {
        return Status == ReservationStatus.ACTIVE && ExpirationDate > DateTime.UtcNow;
    }
}
