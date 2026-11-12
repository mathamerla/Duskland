using System;
using Duskland.Api.Extensions;
using Duskland.Api.Modules.Inventory.Requests;
using Duskland.Modules.Inventory.Contracts.Features.AddStock;
using Duskland.Modules.Inventory.Contracts.Features.AddStock.Command;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock.Command;
using Duskland.Modules.Inventory.Contracts.Features.GetStock;
using Duskland.Modules.Inventory.Contracts.Features.GetStock.Dtos;
using Duskland.Modules.Inventory.Contracts.Features.GetStock.Queries;
using Duskland.Modules.Inventory.Contracts.Features.RemoveStock;
using Duskland.Modules.Inventory.Contracts.Features.RemoveStock.Command;

namespace Duskland.Api.Modules.Inventory.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stores/{storeId}/inventory")
                       .WithTags("Inventory");

        group.MapPost("/", CreateStock)
             .WithName("CreateStock")
             .Produces(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status409Conflict); 

        group.MapGet("/{productId}/", GetStock)
             .WithName("GetStock")
             .Produces<StockDto>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{productId}/add/", AddStock)
             .WithName("AddStock")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{productId}/remove/", RemoveStock)
             .WithName("RemoveStock")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        // group.MapPut("/{productId}/quantity", PatchProduct)
        //      .WithName("SetQuantity")
        //      .Produces<NoContentResult>(StatusCodes.Status204NoContent)
        //      .ProducesProblem(StatusCodes.Status400BadRequest)
        //      .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        //      .ProducesProblem(StatusCodes.Status428PreconditionRequired)
        //      .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateStock(Guid storeId, Guid productId, CreateStockRequest request, ICreateStockHandler handler, CancellationToken cancellationToken)
    {
        var command = new CreateStockCommand(storeId, productId, request.Quantity);
        await handler.Handle(command, cancellationToken);

        return Results.Created($"/api/stores/{storeId}/inventory/{productId}", (object?)null);
    }

     private static async Task<StockDto> GetStock(Guid storeId, Guid productId, IGetStockHandler handler, HttpContext context, CancellationToken token)
    {
        var query = new GetStockQuery(storeId, productId);

        var result = await handler.Handle(query, token);

        context.AddETagToResponse(result.Version);

        return result.Data;
    }

    private static async Task<IResult> AddStock(Guid storeId, Guid productId, AddStockRequest request, IAddStockHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        var command = new AddStockCommand(storeId, productId, request.StockToAdd);

        await handler.Handle(command, cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> RemoveStock(Guid storeId, Guid productId, RemoveStockRequest request, IRemoveStockHandler handler, CancellationToken cancellationToken)
    {
        var command = new RemoveStockCommand(storeId, productId, request.StockToRemove);

        await handler.Handle(command, cancellationToken);

        return Results.NoContent();
    }
}
