using Duskland.BuildingBlocks.Domain.Exceptions;

namespace Duskland.Modules.Inventory.Domain;

public class Stock
{
    public static Stock Create(Guid storeId, Guid productId, int quantity)
    {
        var stock = new Stock()
        {
            Id = Guid.NewGuid(),
            StoreId = storeId,
            ProductId = productId,
            ReservedQuantity = 0
        };

        stock.SetQuantity(quantity);

        return stock;
    }

    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }
    public int ReservedQuantity { get; private set; }

    public Guid Version { get; private set; }

    public ICollection<StockReservation> Reservations { get; private set; } = new List<StockReservation>();

    public int AvailableQuantity => Quantity - ReservedQuantity;

    public void SetQuantity(int quantity)
    {
        if (quantity < 0)
        {
            throw new DomainException("Quantity cannot be negative.");
        }

        if (quantity < ReservedQuantity)
        {
            throw new DomainException("Quantity cannot be lower than reserved quantity.");
        }

        Quantity = quantity;
        ElevateVersion();
    }

    public void RemoveStock(int quantity)
    {
        if (quantity < 0)
        {
            throw new DomainException("Quantity cannot be negative.");
        }

        if (quantity > AvailableQuantity)
        {
            throw new DomainException("Quantity cannot be lower than reserved quantity.");
        }

        Quantity -= quantity;
        ElevateVersion();
    }

    public void AddStock(int quantity)
    {
        if (quantity < 0)
        {
            throw new DomainException("Quantity to add cannot be negative.");
        }

        Quantity += quantity;
        ElevateVersion();
    }

    public void ElevateVersion()
    {
        Version = Guid.NewGuid();
    }
}
