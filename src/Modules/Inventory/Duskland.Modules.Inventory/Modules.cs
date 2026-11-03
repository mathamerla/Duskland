using Duskland.Modules.Inventory.Infrastructure.Context;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock;
using Duskland.Modules.Inventory.Features.CreateStock;
using EntityFramework.Exceptions.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duskland.Modules.Inventory;

public static class InventoryModules
{
    public static IServiceCollection RegisterModules(this IServiceCollection services, string connectionString)
    {
         services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(connectionString, 
                x => x.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.SCHEMA)  
            ).UseExceptionProcessor()
        ); 

        services.AddScoped<ICreateStockHandler, CreateStockHandler>();

        return services;
    }
}
