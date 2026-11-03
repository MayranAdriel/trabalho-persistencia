using Microsoft.EntityFrameworkCore;
using TwoPhaseCommitDemo.Domain.Entities;

namespace TwoPhaseCommitDemo.Infrastructure.Data;

public class PostgresDbContext : DbContext
{
    public PostgresDbContext(DbContextOptions<PostgresDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.Sku)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(e => e.Sku)
                .IsUnique();

            entity.Property(e => e.Name)
                .HasMaxLength(250)
                .IsRequired();

            entity.Property(e => e.Price)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(e => e.StockQuantity)
                .IsRequired();

            entity.Property(e => e.Version)
                .IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired();

            entity.Property(e => e.UpdatedAtUtc);
        });
    }
}

