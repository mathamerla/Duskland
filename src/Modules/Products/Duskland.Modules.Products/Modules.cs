using Duskland.Modules.Products.Infrastructure.DAL.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EntityFramework.Exceptions.PostgreSQL;
using Duskland.Modules.Products.Features.CreateProduct;
using Duskland.Modules.Products.Contracts.Features.CreateProduct;
using Duskland.Modules.Products.Contracts.Features.GetProducts.Queries;
using Duskland.Modules.Products.Features.GetProducts;

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

        return services;
    } 
}
