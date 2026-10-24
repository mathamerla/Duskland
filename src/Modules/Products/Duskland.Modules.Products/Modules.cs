using Duskland.Modules.Products.Infrastructure.DAL.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EntityFramework.Exceptions.PostgreSQL;
using Duskland.Modules.Products.Features.CreateProduct;
using Duskland.Modules.Products.Contracts.Features.CreateProduct;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Queries;
using Duskland.Modules.Products.Features.GetProducts;
using Duskland.Modules.Products.Contracts.Features.UpdateProduct;
using Duskland.Modules.Products.Features.UpdateProduct;
using Duskland.Modules.Products.Contracts.Features.GetProduct;
using Duskland.Modules.Products.Features.GetProduct;

namespace Duskland.Modules.Products;

public static class ProductModules
{
    public static IServiceCollection RegisterModules(this IServiceCollection services, string connectionString)
    {
         services.AddDbContext<ProductDbContext>(options =>
            options.UseNpgsql(connectionString, 
                x => x.MigrationsHistoryTable("__EFMigrationsHistory", ProductDbContext.SCHEMA)  
            ).UseExceptionProcessor()
        ); 

        services.AddScoped<ICreateProductHandler, CreateProductHandler>();
        services.AddScoped<IGetProductsHandler, GetProductsHandler>();
        services.AddScoped<IUpdateProductHandler, UpdateProductHandler>();
        services.AddScoped<IGetProductHandler, GetProductHandler>();

        return services;
    } 
}
