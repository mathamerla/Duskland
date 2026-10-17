using Duskland.Modules.Products.Domain;
using Duskland.Modules.Products.Infrastructure.DAL.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Duskland.Modules.Products.Infrastructure.DAL.Context;

internal class ProductDbContext: DbContext
{
    public const string SCHEMA = "product";

    public ProductDbContext(DbContextOptions<ProductDbContext> options): base(options)
    {
        
    }

    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SCHEMA);

        modelBuilder.Entity<Product>(entity =>
        {
             entity.Property(e => e.SKU)
                .HasMaxLength(50);

             entity.Property(e => e.Name)
                .HasMaxLength(200);

            entity.HasIndex(e => new { e.StoreId, e.SKU })
                .HasDatabaseName("IX_Products_StoreId_SKU")
                .IsUnique();
        });
    }
}
