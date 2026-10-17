using Duskland.Modules.Products.Infrastructure.DAL.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duskland.Modules.Products;

public static class ProductModules
{
    public static IServiceCollection RegisterModules(this IServiceCollection services, string connectionString)
    {
         services.AddDbContext<ProductDbContext>(options =>
            options.UseNpgsql(connectionString, 
                x => x.MigrationsHistoryTable("__EFMigrationsHistory", ProductDbContext.SCHEMA)  
        )); 

        return services;
    } 
}
