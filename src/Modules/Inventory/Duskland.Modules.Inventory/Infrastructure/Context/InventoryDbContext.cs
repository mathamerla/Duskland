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
    public DbSet<StockReservation> StockReservations { get; internal set; }

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

            entity.HasMany(s => s.Reservations)
                .WithOne(r => r.Stock)
                .HasForeignKey(r => r.StockId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockReservation>(entity =>
        {
            entity.HasIndex(e => new { e.StoreId, e.ProductId, e.OrderId })
                .HasDatabaseName("IX_StockReservations_StoreId_ProductId_OrderId")
                .IsUnique();

            entity.HasIndex(e => new { e.Status, e.ExpirationDate })
                .HasDatabaseName("IX_StockReservations_Status_ExpirationDate");

            entity.ToTable(t => t.HasCheckConstraint("CK_StockReservations_Quantity", "\"Quantity\" > 0"));
        });
    }
}
