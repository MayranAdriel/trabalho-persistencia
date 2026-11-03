using TwoPhaseCommitDemo.Domain.Entities;

namespace TwoPhaseCommitDemo.Domain.Interfaces;

public interface IProductPostgresRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task InsertDirectAsync(Product product, CancellationToken cancellationToken = default);
    Task UpdateDirectAsync(Product product, CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);

    Task<ITransactionParticipant> CreateInsertParticipantAsync(
        Product product,
        bool simulateError = false,
        CancellationToken cancellationToken = default);

    Task<ITransactionParticipant> CreateUpdateParticipantAsync(
        Product product,
        bool simulateError = false,
        CancellationToken cancellationToken = default);
}

