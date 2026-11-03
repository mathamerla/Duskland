using Duskland.BuildingBlocks.Domain.Exceptions;
using Duskland.Modules.Inventory.Domain;

namespace Duskland.Modules.Inventory.Tests;

public class StockTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Create_ValidQuantity_InitializesStock(int quantity)
    {
        var storeId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var stock = Stock.Create(storeId, productId, quantity);

        Assert.NotEqual(Guid.Empty, stock.Id);
        Assert.Equal(storeId, stock.StoreId);
        Assert.Equal(productId, stock.ProductId);
        Assert.Equal(quantity, stock.Quantity);
        Assert.Equal(0, stock.ReservedQuantity);
        Assert.Equal(quantity, stock.AvailableQuantity);
        Assert.NotEqual(Guid.Empty, stock.Version);
    }

    [Fact]
    public void Create_NegativeQuantity_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() =>
            Stock.Create(Guid.NewGuid(), Guid.NewGuid(), -1));

        Assert.Equal("Quantity cannot be negative.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(20)]
    public void SetQuantity_ValidQuantity_ReplacesQuantityAndChangesVersion(int quantity)
    {
        var stock = CreateStock();
        var version = stock.Version;

        stock.SetQuantity(quantity);

        Assert.Equal(quantity, stock.Quantity);
        Assert.Equal(quantity, stock.AvailableQuantity);
        Assert.NotEqual(version, stock.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void AddStock_WithReservations_IncreasesQuantityAndPreservesReservations(int quantity)
    {
        var stock = CreateStock();
        SetReservedQuantity(stock, 3);
        var version = stock.Version;

        stock.AddStock(quantity);

        Assert.Equal(10 + quantity, stock.Quantity);
        Assert.Equal(3, stock.ReservedQuantity);
        Assert.Equal(7 + quantity, stock.AvailableQuantity);
        Assert.NotEqual(version, stock.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(10)]
    public void RemoveStock_AvailableQuantity_DecreasesQuantityAndChangesVersion(int quantity)
    {
        var stock = CreateStock();
        var version = stock.Version;

        stock.RemoveStock(quantity);

        Assert.Equal(10 - quantity, stock.Quantity);
        Assert.Equal(0, stock.ReservedQuantity);
        Assert.Equal(10 - quantity, stock.AvailableQuantity);
        Assert.NotEqual(version, stock.Version);
    }

    [Fact]
    public void RemoveStock_RepeatedRemovals_UsesRemainingAvailableQuantity()
    {
        var stock = CreateStock();

        stock.RemoveStock(3);
        stock.RemoveStock(7);

        Assert.Equal(0, stock.Quantity);
        Assert.Equal(0, stock.ReservedQuantity);
        Assert.Equal(0, stock.AvailableQuantity);
    }

    [Theory]
    [InlineData(0, 11)]
    [InlineData(4, 7)]
    [InlineData(10, 1)]
    public void RemoveStock_InsufficientAvailableQuantity_ThrowsWithoutChangingStock(int reserved, int quantity)
    {
        var stock = CreateStock();
        SetReservedQuantity(stock, reserved);
        var version = stock.Version;

        var exception = Assert.Throws<DomainException>(() => stock.RemoveStock(quantity));

        Assert.Equal("Quantity cannot be lower than reserved quantity.", exception.Message);

        Assert.Equal(10, stock.Quantity);
        Assert.Equal(reserved, stock.ReservedQuantity);
        Assert.Equal(10 - reserved, stock.AvailableQuantity);
        Assert.Equal(version, stock.Version);
    }

    [Theory]
    [InlineData("SetQuantity", "Quantity cannot be negative.")]
    [InlineData("AddStock", "Quantity to add cannot be negative.")]
    [InlineData("RemoveStock", "Quantity cannot be negative.")]
    public void ChangeQuantity_NegativeQuantity_ThrowsWithoutChangingStock(string operation, string expectedMessage)
    {
        var stock = CreateStock();
        SetReservedQuantity(stock, 3);
        var version = stock.Version;
        Action change = operation switch
        {
            "SetQuantity" => () => stock.SetQuantity(-1),
            "AddStock" => () => stock.AddStock(-1),
            _ => () => stock.RemoveStock(-1)
        };

        var exception = Assert.Throws<DomainException>(change);

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(10, stock.Quantity);
        Assert.Equal(3, stock.ReservedQuantity);
        Assert.Equal(7, stock.AvailableQuantity);
        Assert.Equal(version, stock.Version);
    }

    [Fact]
    public void SetQuantity_WithReservations_PreservesReservations()
    {
        var stock = CreateStock();
        SetReservedQuantity(stock, 3);

        stock.SetQuantity(15);

        Assert.Equal(15, stock.Quantity);
        Assert.Equal(3, stock.ReservedQuantity);
        Assert.Equal(12, stock.AvailableQuantity);
    }

    [Fact]
    public void ElevateVersion_ExistingStock_ChangesVersion()
    {
        var stock = CreateStock();
        var version = stock.Version;

        stock.ElevateVersion();

        Assert.NotEqual(Guid.Empty, stock.Version);
        Assert.NotEqual(version, stock.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void SetQuantity_BelowReservedQuantity_ThrowsWithoutChangingStock(int quantity)
    {
        var stock = CreateStock();
        SetReservedQuantity(stock, 3);
        var version = stock.Version;

        var exception = Assert.Throws<DomainException>(() => stock.SetQuantity(quantity));

        Assert.Equal("Quantity cannot be lower than reserved quantity.", exception.Message);
        Assert.Equal(10, stock.Quantity);
        Assert.Equal(3, stock.ReservedQuantity);
        Assert.Equal(7, stock.AvailableQuantity);
        Assert.Equal(version, stock.Version);
    }

    [Fact]
    public void SetQuantity_EqualToReservedQuantity_LeavesNoAvailableStock()
    {
        var stock = CreateStock();
        SetReservedQuantity(stock, 3);
        var version = stock.Version;

        stock.SetQuantity(3);

        Assert.Equal(3, stock.Quantity);
        Assert.Equal(3, stock.ReservedQuantity);
        Assert.Equal(0, stock.AvailableQuantity);
        Assert.NotEqual(version, stock.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(7)]
    public void RemoveStock_WithReservations_PreservesReservedStock(int quantity)
    {
        var stock = CreateStock();
        SetReservedQuantity(stock, 3);
        var version = stock.Version;

        stock.RemoveStock(quantity);

        Assert.Equal(10 - quantity, stock.Quantity);
        Assert.Equal(3, stock.ReservedQuantity);
        Assert.Equal(7 - quantity, stock.AvailableQuantity);
        Assert.NotEqual(version, stock.Version);
    }

    // Reservations have no public mutation method yet; seed state as EF would when loading stock.
    private static void SetReservedQuantity(Stock stock, int quantity)
        => typeof(Stock).GetProperty(nameof(Stock.ReservedQuantity))!.SetValue(stock, quantity);

    private static Stock CreateStock() => Stock.Create(Guid.NewGuid(), Guid.NewGuid(), 10);
}
