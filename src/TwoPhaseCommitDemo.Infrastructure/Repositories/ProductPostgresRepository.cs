using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TwoPhaseCommitDemo.Domain.Entities;
using TwoPhaseCommitDemo.Domain.Interfaces;
using TwoPhaseCommitDemo.Infrastructure.Data;
using TwoPhaseCommitDemo.Infrastructure.Transactions;

namespace TwoPhaseCommitDemo.Infrastructure.Repositories;

public class ProductPostgresRepository : IProductPostgresRepository
{
    private readonly IDbContextFactory<PostgresDbContext> _contextFactory;
    private readonly ILogger<ProductPostgresRepository> _logger;

    public ProductPostgresRepository(
        IDbContextFactory<PostgresDbContext> contextFactory,
        ILogger<ProductPostgresRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Products
            .AsNoTracking()
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task InsertDirectAsync(Product product, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDirectAsync(Product product, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Products.Update(product);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Products.RemoveRange(context.Products);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ITransactionParticipant> CreateInsertParticipantAsync(
        Product product,
        bool simulateError = false,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        return new PostgresTransactionParticipant(
            dbContext: dbContext,
            transaction: transaction,
            operation: async (ctx, ct) =>
            {
                await ctx.Products.AddAsync(product, ct);
            },
            simulateError: simulateError,
            logger: _logger
        );
    }

    public async Task<ITransactionParticipant> CreateUpdateParticipantAsync(
        Product product,
        bool simulateError = false,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        return new PostgresTransactionParticipant(
            dbContext: dbContext,
            transaction: transaction,
            operation: async (ctx, ct) =>
            {
                var existing = await ctx.Products.FirstOrDefaultAsync(p => p.Id == product.Id, ct);
                if (existing != null)
                {
                    existing.Name = product.Name;
                    existing.Price = product.Price;
                    existing.StockQuantity = product.StockQuantity;
                    existing.Version = product.Version;
                    existing.UpdatedAtUtc = product.UpdatedAtUtc;
                    ctx.Products.Update(existing);
                }
                else
                {
                    await ctx.Products.AddAsync(product, ct);
                }
            },
            simulateError: simulateError,
            logger: _logger
        );
    }
}

