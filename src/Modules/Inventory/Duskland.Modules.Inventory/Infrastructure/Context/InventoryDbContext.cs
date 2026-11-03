using System;
using Duskland.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Inventory.Infrastructure.Context;

internal class InventoryDbContext: DbContext
{
    public const string SCHEMA = "inventory";

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options): base(options)
    {
        
    }

    public DbSet<Stock> Stocks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SCHEMA);

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasIndex(e => new { e.StoreId, e.ProductId })
                .HasDatabaseName("IX_Stocks_StoreId_ProductId")
                .IsUnique();
                
            entity.Property(p => p.Version)
                .IsConcurrencyToken(); 
        });
    }
}
