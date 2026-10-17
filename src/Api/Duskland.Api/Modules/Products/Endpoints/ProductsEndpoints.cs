using Duskland.Api.Modules.Products.Requests;
using Duskland.Modules.Products.Features.CreateProduct;
using Duskland.Modules.Products.Infrastructure.DAL.Context;

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

        return app;
    }

    private static async Task<IResult> CreateProduct(Guid storeId, CreateProductRequest request, ProductDbContext context, CancellationToken cancellationToken)
    {
        var command = new CreateProductCommand(storeId, request.SKU, request.Name, request.Price);
        await new CreateProductHandler(context).Handle(command, cancellationToken);

        //TO DO: CreatedAt when get endpoint is added
        return Results.Created();
    }
}
