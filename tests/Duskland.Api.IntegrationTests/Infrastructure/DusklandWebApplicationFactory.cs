using Duskland.Modules.Products.Domain;
using Duskland.Modules.Products.Infrastructure.DAL.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Duskland.Api.IntegrationTests.Infrastructure;

public class DusklandWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();


    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:ProductConnection", Container.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync() 
    {
        await Container.StopAsync();
        await Container.DisposeAsync();

        await base.DisposeAsync();
    }

    public void ClearTable<T>() where T: class
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

        context.Set<T>().ExecuteDelete();
    }

    public async Task<IReadOnlyList<T>> GetTable<T>() where T: class
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

        return await context.Set<T>().ToListAsync();
    }

    public async Task InsertIntoTable<T>(IReadOnlyList<T> items) where T: class
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

        await context.Set<T>().AddRangeAsync(items);
        await context.SaveChangesAsync();
    }
}
