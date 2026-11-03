using TwoPhaseCommitDemo.Domain.Common;

namespace TwoPhaseCommitDemo.Domain.Interfaces;

public interface ITwoPhaseCommitCoordinator
{
    Task<Result<TwoPhaseCommitResult>> ExecuteTransactionAsync(
        IEnumerable<ITransactionParticipant> participants,
        CancellationToken cancellationToken = default);
}

