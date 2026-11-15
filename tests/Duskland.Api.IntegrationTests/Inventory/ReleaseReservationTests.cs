using System.Net;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Inventory.Domain.Enums;
using Duskland.Modules.Inventory.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duskland.Api.IntegrationTests.Inventory;

public class ReleaseReservationTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ReleaseReservationTests(DusklandWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        factory.ClearTable<StockReservation>();
        factory.ClearTable<Stock>();
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(7, 3)]
    [InlineData(10, 10)]
    public async Task ReleaseReservation_Active_Returns204AndReleasesReservedStock(int reserved, int quantity)
    {
        var reservation = await SeedAsync(reserved, quantity);

        using var response = await ReleaseAsync(reservation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = Assert.Single(await _factory.GetTable<StockReservation>());
        Assert.Equal(ReservationStatus.RELEASED, saved.Status);
        AssertReservationUnchanged(reservation, saved, ReservationStatus.RELEASED);
        await AssertStockReleasedAsync(reservation.Stock, quantity);
    }

    [Fact]
    public async Task ReleaseReservation_RepeatedRequest_Returns204WithoutUpdatingStockAgain()
    {
        var reservation = await SeedAsync();
        using var first = await ReleaseAsync(reservation);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        await AssertStockReleasedAsync(reservation.Stock, reservation.Quantity);
        var releasedStock = Assert.Single(await _factory.GetTable<Stock>());
        var releasedReservation = Assert.Single(await _factory.GetTable<StockReservation>());

        using var second = await ReleaseAsync(reservation);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        AssertStockUnchanged(releasedStock, Assert.Single(await _factory.GetTable<Stock>()));
        AssertReservationUnchanged(releasedReservation, Assert.Single(await _factory.GetTable<StockReservation>()));
    }

    [Theory]
    [InlineData(ReservationStatus.RELEASED, false, HttpStatusCode.NoContent)]
    [InlineData(ReservationStatus.EXPIRED, false, HttpStatusCode.NoContent)]
    [InlineData(ReservationStatus.CONFIRMED, false, HttpStatusCode.Conflict)]
    [InlineData(ReservationStatus.CONFIRMED, true, HttpStatusCode.Conflict)]
    public async Task ReleaseReservation_ProcessedOrExpired_DoesNotChangeStockOrReservation(
        ReservationStatus status, bool pastExpiration, HttpStatusCode expectedStatus)
    {
        var reservation = await SeedAsync(status: status, pastExpiration: pastExpiration);

        using var response = await ReleaseAsync(reservation);

        Assert.Equal(expectedStatus, response.StatusCode);
        await AssertUnchangedAsync(reservation);
    }

    [Fact]
    public async Task ReleaseReservation_MissingReservation_Returns404WithoutChanges()
    {
        var reservation = await SeedAsync();

        using var response = await ReleaseAsync(reservation, reservationId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertUnchangedAsync(reservation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseReservation_WrongStore_Returns404WithoutChanges(bool stockExistsInWrongStore)
    {
        var reservation = await SeedAsync();
        var wrongStoreId = Guid.NewGuid();
        Stock? otherStock = null;
        if (stockExistsInWrongStore)
        {
            otherStock = Stock.Create(wrongStoreId, reservation.ProductId, 10);
            typeof(Stock).GetProperty(nameof(Stock.ReservedQuantity))!.SetValue(otherStock, 3);
            await _factory.InsertIntoTable([otherStock]);
        }

        using var response = await ReleaseAsync(reservation, storeId: wrongStoreId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertReservationUnchanged(reservation, Assert.Single(await _factory.GetTable<StockReservation>()));
        var stocks = await _factory.GetTable<Stock>();
        Assert.Equal(stockExistsInWrongStore ? 2 : 1, stocks.Count);
        AssertStockUnchanged(reservation.Stock, Assert.Single(stocks, stock => stock.Id == reservation.StockId));
        if (otherStock is not null)
            AssertStockUnchanged(otherStock, Assert.Single(stocks, stock => stock.Id == otherStock.Id));
    }

    [Fact]
    public async Task ReleaseReservation_ConcurrentRequests_BothReturn204AndReleaseStockOnlyOnce()
    {
        var reservation = await SeedAsync();

        var responses = await Task.WhenAll(ReleaseAsync(reservation), ReleaseAsync(reservation));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
            AssertReservationUnchanged(reservation, Assert.Single(await _factory.GetTable<StockReservation>()),
                ReservationStatus.RELEASED);
            await AssertStockReleasedAsync(reservation.Stock, reservation.Quantity);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseReservation_WrongProduct_Returns404WithoutChanges(bool stockExistsForWrongProduct)
    {
        var reservation = await SeedAsync();
        var wrongProductId = Guid.NewGuid();
        Stock? otherStock = null;
        if (stockExistsForWrongProduct)
        {
            otherStock = Stock.Create(reservation.StoreId, wrongProductId, 10);
            typeof(Stock).GetProperty(nameof(Stock.ReservedQuantity))!.SetValue(otherStock, 3);
            await _factory.InsertIntoTable([otherStock]);
        }

        using var response = await ReleaseAsync(reservation, productId: wrongProductId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertReservationUnchanged(reservation, Assert.Single(await _factory.GetTable<StockReservation>()));
        var stocks = await _factory.GetTable<Stock>();
        Assert.Equal(stockExistsForWrongProduct ? 2 : 1, stocks.Count);
        AssertStockUnchanged(reservation.Stock, Assert.Single(stocks, stock => stock.Id == reservation.StockId));
        if (otherStock is not null)
            AssertStockUnchanged(otherStock, Assert.Single(stocks, stock => stock.Id == otherStock.Id));
    }

    [Theory]
    [InlineData(ReservationStatus.RELEASED, true)]
    [InlineData(ReservationStatus.RELEASED, false)]
    [InlineData(ReservationStatus.EXPIRED, true)]
    [InlineData(ReservationStatus.EXPIRED, false)]
    [InlineData(ReservationStatus.CONFIRMED, true)]
    [InlineData(ReservationStatus.CONFIRMED, false)]
    public async Task ReleaseReservation_ProcessedReservationWithWrongIdentifiers_Returns404WithoutChanges(
        ReservationStatus status, bool wrongStore)
    {
        var reservation = await SeedAsync(status: status);

        using var response = await ReleaseAsync(reservation,
            storeId: wrongStore ? Guid.NewGuid() : reservation.StoreId,
            productId: wrongStore ? reservation.ProductId : Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertUnchangedAsync(reservation);
    }

    [Fact]
    public async Task ReleaseReservation_ConcurrentDifferentReservationsOnSameStock_ReleasesBothQuantities()
    {
        var first = await SeedAsync(7, 3);
        var second = await SeedReservationForExistingStockAsync(first.Stock, 4);

        var responses = await Task.WhenAll(ReleaseAsync(first), ReleaseAsync(second));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
            var reservations = await _factory.GetTable<StockReservation>();
            Assert.Equal(2, reservations.Count);
            AssertReservationUnchanged(first, Assert.Single(reservations, item => item.Id == first.Id),
                ReservationStatus.RELEASED);
            AssertReservationUnchanged(second, Assert.Single(reservations, item => item.Id == second.Id),
                ReservationStatus.RELEASED);
            await AssertStockReleasedAsync(first.Stock, first.Quantity + second.Quantity);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task ReleaseReservation_InconsistentReservedQuantity_RollsBackEntireTransaction(int reserved)
    {
        var reservation = await SeedAsync(reserved, 3);

        using var response = await ReleaseAsync(reservation);

        // Verify rollback even when the HTTP status assertion would fail.
        await AssertUnchangedAsync(reservation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReleaseReservation_RetryAfterStockRepair_SucceedsWithoutLosingReservation()
    {
        var reservation = await SeedAsync(2, 3);
        using var failed = await ReleaseAsync(reservation);
        await AssertUnchangedAsync(reservation);
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);

        // Repair the inconsistent data, then retry the same reservation over HTTP.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await context.Stocks.Where(stock => stock.Id == reservation.StockId)
                .ExecuteUpdateAsync(update => update.SetProperty(stock => stock.ReservedQuantity, 3));
        }
        var repairedStock = Assert.Single(await _factory.GetTable<Stock>());

        using var retried = await ReleaseAsync(reservation);

        Assert.Equal(HttpStatusCode.NoContent, retried.StatusCode);
        AssertReservationUnchanged(reservation, Assert.Single(await _factory.GetTable<StockReservation>()),
            ReservationStatus.RELEASED);
        await AssertStockReleasedAsync(repairedStock, reservation.Quantity);
    }

    [Fact]
    public async Task ReleaseReservation_OneOfMultipleReservations_DoesNotAffectOtherReservationsOrStores()
    {
        var reservation = await SeedAsync(6, 3);
        var sameStockReservation = await SeedReservationForExistingStockAsync(reservation.Stock, 3);
        var otherStoreStock = Stock.Create(Guid.NewGuid(), reservation.ProductId, 10);
        var otherStoreReservation = await SeedAsync(stock: otherStoreStock);

        using var response = await ReleaseAsync(reservation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var reservations = await _factory.GetTable<StockReservation>();
        Assert.Equal(3, reservations.Count);
        AssertReservationUnchanged(reservation, Assert.Single(reservations, item => item.Id == reservation.Id),
            ReservationStatus.RELEASED);
        AssertReservationUnchanged(sameStockReservation, Assert.Single(reservations, item => item.Id == sameStockReservation.Id));
        AssertReservationUnchanged(otherStoreReservation, Assert.Single(reservations, item => item.Id == otherStoreReservation.Id));
        var stocks = await _factory.GetTable<Stock>();
        Assert.Equal(2, stocks.Count);
        await AssertStockReleasedAsync(reservation.Stock, reservation.Quantity);
        AssertStockUnchanged(otherStoreStock, Assert.Single(stocks, stock => stock.Id == otherStoreStock.Id));
    }

    private async Task<StockReservation> SeedAsync(int reserved = 3, int quantity = 3,
        ReservationStatus status = ReservationStatus.ACTIVE, bool pastExpiration = false, Stock? stock = null)
    {
        stock ??= Stock.Create(Guid.NewGuid(), Guid.NewGuid(), 10);
        typeof(Stock).GetProperty(nameof(Stock.ReservedQuantity))!.SetValue(stock, reserved);
        var reservation = StockReservation.Create(stock.ProductId, stock.StoreId, Guid.NewGuid(),
            stock, quantity, DateTime.UtcNow.AddHours(1));
        typeof(StockReservation).GetProperty(nameof(StockReservation.Status))!.SetValue(reservation, status);
        if (pastExpiration)
            typeof(StockReservation).GetProperty(nameof(StockReservation.ExpirationDate))!
                .SetValue(reservation, DateTime.UtcNow.AddHours(-1));
        await _factory.InsertIntoTable([reservation]);
        return reservation;
    }

    private async Task<StockReservation> SeedReservationForExistingStockAsync(Stock stock, int quantity)
    {
        var reservation = StockReservation.Create(stock.ProductId, stock.StoreId,
            Guid.NewGuid(), stock, quantity, DateTime.UtcNow.AddHours(1));
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        context.Attach(stock);
        context.StockReservations.Add(reservation);
        await context.SaveChangesAsync();
        return reservation;
    }

    private Task<HttpResponseMessage> ReleaseAsync(StockReservation reservation, Guid? storeId = null,
        Guid? reservationId = null, Guid? productId = null)
        => _client.PostAsync($"/api/stores/{storeId ?? reservation.StoreId}/inventory/reservation/{reservationId ?? reservation.Id}/release?productId={productId ?? reservation.ProductId}", null);

    private async Task AssertUnchangedAsync(StockReservation reservation)
    {
        AssertStockUnchanged(reservation.Stock, Assert.Single(await _factory.GetTable<Stock>()));
        AssertReservationUnchanged(reservation, Assert.Single(await _factory.GetTable<StockReservation>()));
    }

    private async Task AssertStockReleasedAsync(Stock original, int quantity)
    {
        var saved = Assert.Single(await _factory.GetTable<Stock>(), stock => stock.Id == original.Id);
        Assert.Equal(original.StoreId, saved.StoreId);
        Assert.Equal(original.ProductId, saved.ProductId);
        Assert.Equal(original.Quantity, saved.Quantity);
        Assert.Equal(original.ReservedQuantity - quantity, saved.ReservedQuantity);
        Assert.NotEqual(original.Version, saved.Version);
    }

    private static void AssertStockUnchanged(Stock expected, Stock actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.StoreId, actual.StoreId);
        Assert.Equal(expected.ProductId, actual.ProductId);
        Assert.Equal(expected.Quantity, actual.Quantity);
        Assert.Equal(expected.ReservedQuantity, actual.ReservedQuantity);
        Assert.Equal(expected.Version, actual.Version);
    }

    private static void AssertReservationUnchanged(StockReservation expected, StockReservation actual,
        ReservationStatus? status = null)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.StockId, actual.StockId);
        Assert.Equal(expected.StoreId, actual.StoreId);
        Assert.Equal(expected.ProductId, actual.ProductId);
        Assert.Equal(expected.OrderId, actual.OrderId);
        Assert.Equal(expected.Quantity, actual.Quantity);
        Assert.Equal(status ?? expected.Status, actual.Status);
        Assert.InRange(actual.CreatedAt, expected.CreatedAt.AddTicks(-10), expected.CreatedAt.AddTicks(10));
        Assert.InRange(actual.ExpirationDate, expected.ExpirationDate.AddTicks(-10), expected.ExpirationDate.AddTicks(10));
    }
}
