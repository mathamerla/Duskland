using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Duskland.Api.IntegrationTests.Infrastructure;
using Duskland.Api.Modules.Inventory.Requests;
using Duskland.Modules.Inventory.Domain;
using Duskland.Modules.Inventory.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Duskland.Api.IntegrationTests.Inventory;

public class CreateReservationTests : IClassFixture<DusklandWebApplicationFactory>
{
    private readonly DusklandWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CreateReservationTests(DusklandWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        factory.ClearTable<Stock>();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 10)]
    [InlineData(3, 7)]
    public async Task CreateReservation_AvailableStock_Returns201AndPersistsReservation(int reserved, int quantity)
    {
        var stock = await SeedStockAsync(reserved);
        var orderId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        using var response = await CreateAsync(stock, orderId, quantity);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = Assert.Single(await _factory.GetTable<StockReservation>());
        Assert.NotEqual(Guid.Empty, reservation.Id);
        await AssertReservationIdAsync(response, reservation.Id);
        Assert.Equal(stock.Id, reservation.StockId);
        Assert.Equal(stock.StoreId, reservation.StoreId);
        Assert.Equal(stock.ProductId, reservation.ProductId);
        Assert.Equal(orderId, reservation.OrderId);
        Assert.Equal(quantity, reservation.Quantity);
        Assert.Equal(ReservationStatus.ACTIVE, reservation.Status);
        Assert.InRange(reservation.CreatedAt, before, DateTime.UtcNow);
        Assert.InRange(reservation.ExpirationDate, before.AddHours(1), DateTime.UtcNow.AddHours(1));
        var saved = Assert.Single(await _factory.GetTable<Stock>());
        Assert.Equal(10, saved.Quantity);
        Assert.Equal(reserved + quantity, saved.ReservedQuantity);
        Assert.Equal(10 - reserved - quantity, saved.AvailableQuantity);
        Assert.NotEqual(stock.Version, saved.Version);
    }

    [Theory]
    [InlineData(ReservationStatus.ACTIVE, 3, false, HttpStatusCode.OK)]
    [InlineData(ReservationStatus.CONFIRMED, 3, false, HttpStatusCode.Conflict)]
    [InlineData(ReservationStatus.ACTIVE, 4, false, HttpStatusCode.Conflict)]
    [InlineData(ReservationStatus.CONFIRMED, 4, false, HttpStatusCode.Conflict)]
    [InlineData(ReservationStatus.RELEASED, 3, false, HttpStatusCode.Conflict)]
    [InlineData(ReservationStatus.EXPIRED, 3, false, HttpStatusCode.Conflict)]
    [InlineData(ReservationStatus.ACTIVE, 3, true, HttpStatusCode.Conflict)]
    public async Task CreateReservation_ExistingReservation_DoesNotChangeStockOrReservation(
        ReservationStatus status, int quantity, bool expired, HttpStatusCode expectedStatus)
    {
        var stock = await SeedStockAsync(3);
        var reservation = StockReservation.Create(stock.ProductId, stock.StoreId, Guid.NewGuid(),
            stock, 3, DateTime.UtcNow.AddHours(1));
        typeof(StockReservation).GetProperty(nameof(StockReservation.Status))!.SetValue(reservation, status);
        if (expired)
        {
            typeof(StockReservation).GetProperty(nameof(StockReservation.ExpirationDate))!
                .SetValue(reservation, DateTime.UtcNow.AddHours(-1));
        }
        // Insert the reservation without trying to insert its already persisted stock again.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Duskland.Modules.Inventory.Infrastructure.Context.InventoryDbContext>();
            context.Attach(stock);
            context.StockReservations.Add(reservation);
            await context.SaveChangesAsync();
        }

        using var response = await CreateAsync(stock, reservation.OrderId, quantity);

        Assert.Equal(expectedStatus, response.StatusCode);
        var savedStock = Assert.Single(await _factory.GetTable<Stock>());
        Assert.Equal(stock.Quantity, savedStock.Quantity);
        Assert.Equal(stock.ReservedQuantity, savedStock.ReservedQuantity);
        Assert.Equal(stock.Version, savedStock.Version);
        var saved = Assert.Single(await _factory.GetTable<StockReservation>());
        Assert.Equal(reservation.Id, saved.Id);
        if (expectedStatus == HttpStatusCode.OK)
        {
            await AssertReservationIdAsync(response, reservation.Id);
        }
        Assert.Equal(3, saved.Quantity);
        Assert.Equal(status, saved.Status);
        Assert.InRange(saved.ExpirationDate, reservation.ExpirationDate.AddTicks(-10), reservation.ExpirationDate.AddTicks(10));
    }

    [Theory]
    [InlineData(0, 11)]
    [InlineData(3, 8)]
    [InlineData(10, 1)]
    public async Task CreateReservation_InsufficientStock_Returns400WithoutChanges(int reserved, int quantity)
    {
        var stock = await SeedStockAsync(reserved);

        using var response = await CreateAsync(stock, Guid.NewGuid(), quantity);

        await AssertBadRequestAsync(response, "Not enough stock available to make reservation");
        Assert.Equivalent(stock, Assert.Single(await _factory.GetTable<Stock>()));
        Assert.Empty(await _factory.GetTable<StockReservation>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateReservation_NonPositiveQuantity_Returns400WithoutChanges(int quantity)
    {
        var stock = await SeedStockAsync();

        using var response = await CreateAsync(stock, Guid.NewGuid(), quantity);

        await AssertBadRequestAsync(response, "Quantity must be greater than zero");
        Assert.Equivalent(stock, Assert.Single(await _factory.GetTable<Stock>()));
        Assert.Empty(await _factory.GetTable<StockReservation>());
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(3, 4)]
    public async Task CreateReservation_ConcurrentRequestsExceedAvailableStock_OnlyOneSucceeds(int reserved, int quantity)
    {
        var stock = await SeedStockAsync(reserved);
        var orderIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        // Either request fits on its own, but both cannot be fulfilled.
        var responses = await Task.WhenAll(
            CreateAsync(stock, orderIds[0], quantity),
            CreateAsync(stock, orderIds[1], quantity));
        try
        {
            var successfulResponse = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            var rejectedResponse = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
            await AssertBadRequestAsync(rejectedResponse, "Not enough stock available to make reservation");

            var reservation = Assert.Single(await _factory.GetTable<StockReservation>());
            await AssertReservationIdAsync(successfulResponse, reservation.Id);
            Assert.Equal(orderIds[Array.IndexOf(responses, successfulResponse)], reservation.OrderId);
            Assert.Equal(stock.Id, reservation.StockId);
            Assert.Equal(stock.StoreId, reservation.StoreId);
            Assert.Equal(stock.ProductId, reservation.ProductId);
            Assert.Equal(quantity, reservation.Quantity);
            Assert.Equal(ReservationStatus.ACTIVE, reservation.Status);

            var saved = Assert.Single(await _factory.GetTable<Stock>());
            Assert.Equal(stock.Quantity, saved.Quantity);
            Assert.Equal(reserved + quantity, saved.ReservedQuantity);
            Assert.Equal(stock.Quantity - reserved - quantity, saved.AvailableQuantity);
            Assert.NotEqual(stock.Version, saved.Version);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Fact]
    public async Task CreateReservation_MissingStock_Returns404WithoutReservation()
    {
        var stock = Stock.Create(Guid.NewGuid(), Guid.NewGuid(), 10);

        using var response = await CreateAsync(stock, Guid.NewGuid(), 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await _factory.GetTable<Stock>());
        Assert.Empty(await _factory.GetTable<StockReservation>());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(10)]
    public async Task CreateReservation_ConcurrentRequestsForSameOrder_ReservesStockOnlyOnce(int quantity)
    {
        var stock = await SeedStockAsync();
        var orderId = Guid.NewGuid();

        var responses = await Task.WhenAll(
            CreateAsync(stock, orderId, quantity),
            CreateAsync(stock, orderId, quantity));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            var duplicate = Assert.Single(responses, response => response.StatusCode != HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

            var reservation = Assert.Single(await _factory.GetTable<StockReservation>());
            foreach (var response in responses)
            {
                await AssertReservationIdAsync(response, reservation.Id);
            }
            Assert.Equal(orderId, reservation.OrderId);
            Assert.Equal(quantity, reservation.Quantity);
            var saved = Assert.Single(await _factory.GetTable<Stock>());
            Assert.Equal(stock.Quantity, saved.Quantity);
            Assert.Equal(quantity, saved.ReservedQuantity);
            Assert.Equal(stock.Quantity - quantity, saved.AvailableQuantity);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Fact]
    public async Task CreateReservation_ConcurrentRequestsForSameOrderWithDifferentQuantities_ReturnsConflictAndReservesOnlyWinningQuantity()
    {
        var stock = await SeedStockAsync();
        var orderId = Guid.NewGuid();
        var quantities = new[] { 3, 4 };

        var responses = await Task.WhenAll(
            CreateAsync(stock, orderId, quantities[0]),
            CreateAsync(stock, orderId, quantities[1]));
        try
        {
            var created = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            var winningQuantity = quantities[Array.IndexOf(responses, created)];
            var reservation = Assert.Single(await _factory.GetTable<StockReservation>());
            Assert.Equal(orderId, reservation.OrderId);
            Assert.Equal(winningQuantity, reservation.Quantity);
            await AssertReservationIdAsync(created, reservation.Id);
            var saved = Assert.Single(await _factory.GetTable<Stock>());
            Assert.Equal(winningQuantity, saved.ReservedQuantity);
            Assert.Equal(stock.Quantity - winningQuantity, saved.AvailableQuantity);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    private async Task<Stock> SeedStockAsync(int reserved = 0)
    {
        var stock = Stock.Create(Guid.NewGuid(), Guid.NewGuid(), 10);
        typeof(Stock).GetProperty(nameof(Stock.ReservedQuantity))!.SetValue(stock, reserved);
        await _factory.InsertIntoTable([stock]);
        return stock;
    }

    private Task<HttpResponseMessage> CreateAsync(Stock stock, Guid orderId, int quantity)
        => _client.PostAsJsonAsync($"/api/stores/{stock.StoreId}/inventory/reservation?productId={stock.ProductId}",
            new CreateReservationRequest(orderId, quantity));

    private static async Task AssertReservationIdAsync(HttpResponseMessage response, Guid expectedId)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedId, body.GetProperty("reservationId").GetGuid());
    }

    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string detail)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Equal(detail, problem.Detail);
    }
}
