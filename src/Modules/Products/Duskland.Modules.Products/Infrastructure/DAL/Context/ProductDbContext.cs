using Duskland.Modules.Products.Domain;
using Duskland.Modules.Products.Infrastructure.DAL.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Duskland.Modules.Products.Infrastructure.DAL.Context;

/// <summary>
/// Temp solution - public Context, later when MediatR like solution is implemented  and CQRS handlers will be changed to internal, context also will be switched
/// </summary>
public class ProductDbContext: DbContext
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

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException is PostgresException postgresException && 
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation) // code "23505"
            {
                var constraintName = postgresException.ConstraintName ?? "Unique constraint";
                
                throw new ConflictException($"Conflict on constraint: {constraintName}", ex, constraintName);
            }

            throw;
        }
    }
}
