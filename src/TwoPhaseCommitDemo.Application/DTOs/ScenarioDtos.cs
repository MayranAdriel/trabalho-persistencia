using TwoPhaseCommitDemo.Domain.Common;

namespace TwoPhaseCommitDemo.Application.DTOs;

public record ScenarioResultDto(
    string ScenarioName,
    bool Success,
    string Message,
    TwoPhaseCommitResult? TransactionDetails,
    DatabaseStateSummaryDto BeforeExecution,
    DatabaseStateSummaryDto AfterExecution
);

public record DatabaseStateSummaryDto(
    int PostgresCount,
    int MongoCount,
    List<ProductResponseDto> PostgresProducts,
    List<ProductResponseDto> MongoProducts
);

public record MonitoringSnapshotDto(
    long ReadingIndex,
    DateTime TimestampUtc,
    int PostgresCount,
    int MongoCount,
    bool IsInSync,
    string Details
);

public record Scenario3ExecutionResultDto(
    ScenarioResultDto ScenarioResult,
    List<MonitoringSnapshotDto> ConcurrentReadingsSample
);

