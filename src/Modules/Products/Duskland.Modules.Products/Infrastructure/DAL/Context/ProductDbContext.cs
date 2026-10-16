using System;
using Duskland.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;

namespace Duskland.Modules.Products.Infrastructure.DAL.Context;

public class ProductDbContext: DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options): base(options)
    {
        
    }

    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
             entity.Property(e => e.SKU)
                .HasMaxLength(50);

             entity.Property(e => e.Name)
                .HasMaxLength(200);
        });
    }

}
