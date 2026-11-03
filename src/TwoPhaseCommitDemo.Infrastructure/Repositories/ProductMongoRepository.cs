using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using TwoPhaseCommitDemo.Domain.Entities;
using TwoPhaseCommitDemo.Domain.Interfaces;
using TwoPhaseCommitDemo.Infrastructure.Data;
using TwoPhaseCommitDemo.Infrastructure.Transactions;

namespace TwoPhaseCommitDemo.Infrastructure.Repositories;

public class ProductMongoRepository : IProductMongoRepository
{
    private readonly MongoDbContext _mongoDbContext;
    private readonly ILogger<ProductMongoRepository> _logger;

    public ProductMongoRepository(
        MongoDbContext mongoDbContext,
        ILogger<ProductMongoRepository> logger)
    {
        _mongoDbContext = mongoDbContext;
        _logger = logger;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _mongoDbContext.Products
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _mongoDbContext.Products
            .Find(_ => true)
            .SortBy(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task InsertDirectAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _mongoDbContext.Products.InsertOneAsync(product, cancellationToken: cancellationToken);
    }

    public async Task UpdateDirectAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _mongoDbContext.Products.ReplaceOneAsync(
            p => p.Id == product.Id,
            product,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken
        );
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await _mongoDbContext.Products.DeleteManyAsync(_ => true, cancellationToken);
    }

    public async Task<ITransactionParticipant> CreateInsertParticipantAsync(
        Product product,
        bool simulateError = false,
        CancellationToken cancellationToken = default)
    {
        var session = await _mongoDbContext.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();

        return new MongoTransactionParticipant(
            mongoDbContext: _mongoDbContext,
            session: session,
            operation: async (sess, ctx, ct) =>
            {
                await ctx.Products.InsertOneAsync(sess, product, cancellationToken: ct);
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
        var session = await _mongoDbContext.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();

        return new MongoTransactionParticipant(
            mongoDbContext: _mongoDbContext,
            session: session,
            operation: async (sess, ctx, ct) =>
            {
                await ctx.Products.ReplaceOneAsync(
                    sess,
                    p => p.Id == product.Id,
                    product,
                    new ReplaceOptions { IsUpsert = true },
                    ct
                );
            },
            simulateError: simulateError,
            logger: _logger
        );
    }
}

