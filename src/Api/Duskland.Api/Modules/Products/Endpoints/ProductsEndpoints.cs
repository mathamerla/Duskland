using Duskland.Api.Modules.Products.Requests;
using Duskland.Modules.Products.Contracts.Features.CreateProduct;
using Duskland.Modules.Products.Contracts.Features.CreateProduct.Command;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Queries;
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

        group.MapGet("/{productId}/", GetFakeProduct)
             .WithName("GetProduct")
             .Produces<int>(StatusCodes.Status200OK);

        group.MapGet("/", GetProducts)
             .WithName("GetProducts")
             .Produces<int>(StatusCodes.Status200OK)
             .Produces<int>(StatusCodes.Status400BadRequest);

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

    private static string GetFakeProduct(Guid storeId, Guid productId)
    {
        return $"Fake product {productId} for store {storeId}";
    }
}
