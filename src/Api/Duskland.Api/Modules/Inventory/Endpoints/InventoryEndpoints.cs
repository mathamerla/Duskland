using System;
using Duskland.Api.Modules.Inventory.Requests;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock.Command;

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

        // group.MapGet("/{productId}/", GetProduct)
        //      .WithName("GetStock")
        //      .Produces<ProductDto>(StatusCodes.Status200OK)
        //      .ProducesProblem(StatusCodes.Status404NotFound);

        // group.MapPost("/{productId}/add/", CreateProduct)
        //      .WithName("AddStock")
        //      .Produces(StatusCodes.Status201Created)
        //      .ProducesProblem(StatusCodes.Status400BadRequest)
        //      .ProducesProblem(StatusCodes.Status409Conflict); 

        // group.MapPost("/{productId}/add/", CreateProduct)
        //      .WithName("RemoveStock")
        //      .Produces(StatusCodes.Status201Created)
        //      .ProducesProblem(StatusCodes.Status400BadRequest)
        //      .ProducesProblem(StatusCodes.Status409Conflict); 


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
}
