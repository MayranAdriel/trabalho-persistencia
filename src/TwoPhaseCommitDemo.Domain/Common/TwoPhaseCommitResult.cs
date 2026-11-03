using TwoPhaseCommitDemo.Domain.Enums;

namespace TwoPhaseCommitDemo.Domain.Common;

public sealed record ParticipantLog(string Participant, string Phase, bool Success, string Details, DateTime TimestampUtc);

public sealed class TwoPhaseCommitResult
{
    public Guid TransactionId { get; init; } = Guid.NewGuid();
    public TransactionState FinalState { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ParticipantLog> ExecutionLogs { get; init; } = [];
}

