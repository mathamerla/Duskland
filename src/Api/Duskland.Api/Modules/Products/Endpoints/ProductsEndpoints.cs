using Duskland.Api.Extensions;
using Duskland.Api.Modules.Products.Requests;
using Duskland.Modules.Products.Contracts.Features.CreateProduct;
using Duskland.Modules.Products.Contracts.Features.CreateProduct.Command;
using Duskland.Modules.Products.Contracts.Features.GetProduct;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Queries;
using Duskland.Modules.Products.Contracts.Features.UpdateProduct;
using Duskland.Modules.Products.Contracts.Features.UpdateProduct.Command;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Duskland.Api.Modules.Products.Endpoints;

public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stores/{storeId}/products")
                       .WithTags("Products");

        group.MapPost("/", CreateProduct)
             .WithName("CreateProduct")
             .Produces<int>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status409Conflict); 

        group.MapGet("/{productId}/", GetProduct)
             .WithName("GetProduct")
             .Produces<int>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status404NotFound);


        group.MapGet("/", GetProducts)
             .WithName("GetProducts")
             .Produces<PagedList<ProductDto>>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status400BadRequest);

        
        group.MapPatch("/", PatchProduct)
             .WithName("PatchProduct")
             .Produces<NoContentResult>(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status412PreconditionFailed)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateProduct(Guid storeId, CreateProductRequest request, ICreateProductHandler handler, CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand(storeId, request.SKU, request.Name, request.Price);
        var productId = await handler.Handle(command, cancellationToken);

        return Results.CreatedAtRoute("GetProduct", new { storeId, productId });
    }

    private static async Task<PagedList<ProductDto>> GetProducts(Guid storeId, [AsParameters]PagedQueryParameters parameters, [FromQuery]string? searchBy, IGetProductsHandler handler, CancellationToken token)
    {
        var query = new GetProductsQuery(parameters, storeId, searchBy ?? string.Empty);
        var result = await handler.Handle(query, token);

        return result;
    }

    private static async Task<IResult> PatchProduct(Guid storeId, UpdateProductRequest request, IUpdateProductHandler handler, HttpContext context, CancellationToken token)
    {
        var version = context.GetIfMatchStamp();

        var command = new UpdateProductCommand(request.Id, storeId, request.Name, request.Price, version);

        await handler.Handle(command, token);

        return Results.NoContent();
    }

    private static async Task<ProductDto> GetProduct(Guid storeId, Guid productId, IGetProductHandler handler, HttpContext context, CancellationToken token)
    {
        var query = new GetProductQuery(productId);

        var result = await handler.Handle(query, token);

        context.Response.Headers.ETag = result.Version.ToString();

        return result.Product;
    }
}
