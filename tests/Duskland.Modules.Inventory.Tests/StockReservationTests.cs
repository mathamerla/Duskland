using Duskland.BuildingBlocks.Domain.Exceptions;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Inventory.Domain.Enums;

namespace Duskland.Modules.Inventory.Tests;

public class StockReservationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void Create_ValidRequest_InitializesActiveReservation(int quantity)
    {
        var stock = Stock.Create(Guid.NewGuid(), Guid.NewGuid(), 10);
        var orderId = Guid.NewGuid();
        var before = DateTime.UtcNow;
        var expiration = before.AddHours(1);

        var reservation = StockReservation.Create(stock.ProductId, stock.StoreId, orderId, stock, quantity, expiration);

        Assert.NotEqual(Guid.Empty, reservation.Id);
        Assert.Equal(stock.Id, reservation.StockId);
        Assert.Same(stock, reservation.Stock);
        Assert.Equal(stock.ProductId, reservation.ProductId);
        Assert.Equal(stock.StoreId, reservation.StoreId);
        Assert.Equal(orderId, reservation.OrderId);
        Assert.Equal(quantity, reservation.Quantity);
        Assert.Equal(ReservationStatus.ACTIVE, reservation.Status);
        Assert.InRange(reservation.CreatedAt, before, DateTime.UtcNow);
        Assert.Equal(expiration, reservation.ExpirationDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_NonPositiveQuantity_ThrowsDomainException(int quantity)
    {
        var stock = Stock.Create(Guid.NewGuid(), Guid.NewGuid(), 10);

        var exception = Assert.Throws<DomainException>(() => StockReservation.Create(
            stock.ProductId, stock.StoreId, Guid.NewGuid(), stock, quantity, DateTime.UtcNow.AddHours(1)));

        Assert.Equal("Quantity must be greater than zero", exception.Message);
    }

    [Fact]
    public void Create_PastExpiration_ThrowsDomainException()
    {
        var stock = Stock.Create(Guid.NewGuid(), Guid.NewGuid(), 10);

        var exception = Assert.Throws<DomainException>(() => StockReservation.Create(
            stock.ProductId, stock.StoreId, Guid.NewGuid(), stock, 1, DateTime.UtcNow.AddMinutes(-1)));

        Assert.Equal("Expiration date must be in the future", exception.Message);
    }
}
