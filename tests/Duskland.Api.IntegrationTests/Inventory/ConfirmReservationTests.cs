using System.Net;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Inventory.Domain.Enums;
using Duskland.Modules.Inventory.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duskland.Api.IntegrationTests.Inventory;

public class ConfirmReservationTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ConfirmReservationTests(DusklandWebApplicationFactory factory)
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
    public async Task ConfirmReservation_Active_Returns204AndConsumesReservedStock(int reserved, int quantity)
    {
        var reservation = await SeedAsync(reserved, quantity);
        var before = await ReservationSnapshotAsync();

        using var response = await ConfirmAsync(reservation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(before with { Status = ReservationStatus.CONFIRMED }, await ReservationSnapshotAsync());
        await AssertConsumedAsync(reservation.Stock, quantity);
    }

    [Theory]
    [InlineData(ReservationStatus.ACTIVE, true, HttpStatusCode.Conflict)]
    [InlineData(ReservationStatus.RELEASED, false, HttpStatusCode.Conflict)]
    [InlineData(ReservationStatus.EXPIRED, false, HttpStatusCode.Conflict)]
    [InlineData(ReservationStatus.CONFIRMED, false, HttpStatusCode.NoContent)]
    [InlineData(ReservationStatus.CONFIRMED, true, HttpStatusCode.NoContent)]
    public async Task ConfirmReservation_ExistingStatus_ReturnsExpectedStatusWithoutChanges(
        ReservationStatus status, bool pastExpiration, HttpStatusCode expectedStatus)
    {
        var reservation = await SeedAsync(status: status, pastExpiration: pastExpiration);
        var before = await ReservationSnapshotAsync();

        using var response = await ConfirmAsync(reservation);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(before, await ReservationSnapshotAsync());
        await AssertStockUnchangedAsync(reservation.Stock);
    }

    [Fact]
    public async Task ConfirmReservation_RepeatedRequest_Returns204WithoutConsumingStockAgain()
    {
        var reservation = await SeedAsync();
        using var first = await ConfirmAsync(reservation);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        await AssertConsumedAsync(reservation.Stock, reservation.Quantity);
        var stock = Assert.Single(await _factory.GetTable<Stock>());
        var before = await ReservationSnapshotAsync();

        using var repeated = await ConfirmAsync(reservation);

        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.Equal(before, await ReservationSnapshotAsync());
        await AssertStockUnchangedAsync(stock);
    }

    [Theory]
    [InlineData("reservation", ReservationStatus.ACTIVE)]
    [InlineData("store", ReservationStatus.ACTIVE)]
    [InlineData("product", ReservationStatus.ACTIVE)]
    [InlineData("store", ReservationStatus.CONFIRMED)]
    [InlineData("product", ReservationStatus.CONFIRMED)]
    public async Task ConfirmReservation_WrongIdentifier_Returns404WithoutChanges(string identifier, ReservationStatus status)
    {
        var reservation = await SeedAsync(status: status);
        var before = await ReservationSnapshotAsync();

        using var response = await _client.PostAsync(
            $"/api/stores/{(identifier == "store" ? Guid.NewGuid() : reservation.StoreId)}/inventory/reservation/{(identifier == "reservation" ? Guid.NewGuid() : reservation.Id)}/confirm?productId={(identifier == "product" ? Guid.NewGuid() : reservation.ProductId)}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await ReservationSnapshotAsync());
        await AssertStockUnchangedAsync(reservation.Stock);
    }

    [Theory]
    [InlineData(10, 2)]
    [InlineData(2, 3)]
    [InlineData(8, 10)]
    public async Task ConfirmReservation_InconsistentStock_Returns400AndRollsBackReservation(int quantity, int reserved)
    {
        var reservation = await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await context.Stocks.Where(stock => stock.Id == reservation.StockId)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(stock => stock.Quantity, quantity)
                    .SetProperty(stock => stock.ReservedQuantity, reserved));
        }
        var originalStock = Assert.Single(await _factory.GetTable<Stock>());
        var before = await ReservationSnapshotAsync();

        using var response = await ConfirmAsync(reservation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await ReservationSnapshotAsync());
        await AssertStockUnchangedAsync(originalStock);
    }

    [Fact]
    public async Task ConfirmReservation_ConcurrentRequests_BothReturn204AndConsumeStockOnlyOnce()
    {
        var reservation = await SeedAsync();
        var before = await ReservationSnapshotAsync();

        var responses = await Task.WhenAll(ConfirmAsync(reservation), ConfirmAsync(reservation));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
            Assert.Equal(before with { Status = ReservationStatus.CONFIRMED }, await ReservationSnapshotAsync());
            await AssertConsumedAsync(reservation.Stock, reservation.Quantity);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public async Task ConfirmReservation_ConcurrentRelease_OnlyOneTransitionChangesStock(int reserved)
    {
        var reservation = await SeedAsync(reserved);
        var before = await ReservationSnapshotAsync();
        var responses = await Task.WhenAll(ConfirmAsync(reservation), _client.PostAsync(
            $"/api/stores/{reservation.StoreId}/inventory/reservation/{reservation.Id}/release?productId={reservation.ProductId}", null));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            var saved = await ReservationSnapshotAsync();
            var confirmed = responses[0].StatusCode == HttpStatusCode.NoContent;
            Assert.Equal(before with { Status = confirmed ? ReservationStatus.CONFIRMED : ReservationStatus.RELEASED }, saved);
            Assert.Equal(confirmed ? HttpStatusCode.Conflict : HttpStatusCode.NoContent, responses[1].StatusCode);
            var stock = Assert.Single(await _factory.GetTable<Stock>());
            Assert.Equal(reservation.Stock.Quantity - (confirmed ? reservation.Quantity : 0), stock.Quantity);
            Assert.Equal(reservation.Stock.ReservedQuantity - reservation.Quantity, stock.ReservedQuantity);
            Assert.NotEqual(reservation.Stock.Version, stock.Version);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Fact]
    public async Task ConfirmReservation_OneReservation_DoesNotChangeOtherReservationsOrStocks()
    {
        var reservation = await SeedAsync(7, 3);
        var otherReservation = StockReservation.Create(reservation.ProductId, reservation.StoreId,
            Guid.NewGuid(), reservation.Stock, 4, DateTime.UtcNow.AddHours(1));
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            context.Attach(reservation.Stock);
            context.StockReservations.Add(otherReservation);
            await context.SaveChangesAsync();
        }
        var otherStore = await SeedAsync(stock: Stock.Create(Guid.NewGuid(), reservation.ProductId, 10));
        var otherProduct = await SeedAsync(stock: Stock.Create(reservation.StoreId, Guid.NewGuid(), 10));
        var before = (await _factory.GetTable<StockReservation>()).ToDictionary(r => r.Id, Snapshot);

        using var response = await ConfirmAsync(reservation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await _factory.GetTable<StockReservation>();
        Assert.Equal(4, saved.Count);
        foreach (var item in saved)
            Assert.Equal(item.Id == reservation.Id ? before[item.Id] with { Status = ReservationStatus.CONFIRMED } : before[item.Id], Snapshot(item));
        await AssertConsumedAsync(reservation.Stock, reservation.Quantity);
        await AssertStockUnchangedAsync(otherStore.Stock);
        await AssertStockUnchangedAsync(otherProduct.Stock);
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

    private Task<HttpResponseMessage> ConfirmAsync(StockReservation reservation)
        => _client.PostAsync($"/api/stores/{reservation.StoreId}/inventory/reservation/{reservation.Id}/confirm?productId={reservation.ProductId}", null);

    private async Task AssertConsumedAsync(Stock original, int quantity)
    {
        var saved = Assert.Single(await _factory.GetTable<Stock>(), stock => stock.Id == original.Id);
        Assert.Equal(original.StoreId, saved.StoreId);
        Assert.Equal(original.ProductId, saved.ProductId);
        Assert.Equal(original.Quantity - quantity, saved.Quantity);
        Assert.Equal(original.ReservedQuantity - quantity, saved.ReservedQuantity);
        Assert.Equal(original.AvailableQuantity, saved.AvailableQuantity);
        Assert.NotEqual(original.Version, saved.Version);
    }

    private async Task AssertStockUnchangedAsync(Stock expected)
    {
        var actual = Assert.Single(await _factory.GetTable<Stock>(), stock => stock.Id == expected.Id);
        Assert.Equal(expected.StoreId, actual.StoreId);
        Assert.Equal(expected.ProductId, actual.ProductId);
        Assert.Equal(expected.Quantity, actual.Quantity);
        Assert.Equal(expected.ReservedQuantity, actual.ReservedQuantity);
        Assert.Equal(expected.Version, actual.Version);
    }

    private async Task<ReservationState> ReservationSnapshotAsync()
        => Snapshot(Assert.Single(await _factory.GetTable<StockReservation>()));

    private static ReservationState Snapshot(StockReservation reservation)
        => new(reservation.Id, reservation.StockId, reservation.StoreId, reservation.ProductId, reservation.OrderId,
            reservation.Quantity, reservation.Status, reservation.CreatedAt, reservation.ExpirationDate);

    private record ReservationState(Guid Id, Guid StockId, Guid StoreId, Guid ProductId, Guid OrderId,
        int Quantity, ReservationStatus Status, DateTime CreatedAt, DateTime ExpirationDate);
}
