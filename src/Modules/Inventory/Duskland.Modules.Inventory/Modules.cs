using Duskland.Modules.Inventory.Infrastructure.Context;
using Duskland.Modules.Inventory.Contracts.Features.CreateStock;
using Duskland.Modules.Inventory.Features.CreateStock;
using EntityFramework.Exceptions.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Duskland.Modules.Inventory.Features.GetStock;
using Duskland.Modules.Inventory.Contracts.Features.GetStock;
using Duskland.Modules.Inventory.Features.AddStock;
using Duskland.Modules.Inventory.Contracts.Features.AddStock;
using Duskland.Modules.Inventory.Contracts.Features.RemoveStock;
using Duskland.Modules.Inventory.Features.RemoveStock;

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
        services.AddScoped<IGetStockHandler, GetStockHandler>();
        services.AddScoped<IAddStockHandler, AddStockHandler>();
        services.AddScoped<IRemoveStockHandler, RemoveStockHandler>();

        return services;
    }
}
