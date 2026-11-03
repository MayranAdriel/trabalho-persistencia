using TwoPhaseCommitDemo.Domain.Common;

namespace TwoPhaseCommitDemo.Domain.Interfaces;

public interface ITransactionParticipant : IAsyncDisposable
{
    string Name { get; }
    Task<Result> PrepareAsync(CancellationToken cancellationToken = default);
    Task<Result> CommitAsync(CancellationToken cancellationToken = default);
    Task<Result> RollbackAsync(CancellationToken cancellationToken = default);
}

