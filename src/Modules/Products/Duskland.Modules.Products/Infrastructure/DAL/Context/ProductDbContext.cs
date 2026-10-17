using System;
using Duskland.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;

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
                .IsUnique();
        });
    }

}
