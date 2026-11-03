namespace TwoPhaseCommitDemo.Domain.Enums;

public enum TransactionState
{
    Initialized,
    Preparing,
    Prepared,
    Committing,
    Committed,
    Aborting,
    Aborted
}

