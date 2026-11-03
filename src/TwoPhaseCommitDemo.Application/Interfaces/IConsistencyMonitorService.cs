using TwoPhaseCommitDemo.Application.DTOs;

namespace TwoPhaseCommitDemo.Application.Interfaces;

public interface IConsistencyMonitorService
{
    bool IsRunning { get; }
    void StartMonitoring();
    void StopMonitoring();
    IReadOnlyList<MonitoringSnapshotDto> GetRecentSnapshots(int limit = 50);
    Task<IReadOnlyList<MonitoringSnapshotDto>> CaptureWindowAsync(
        Func<Task> actionDuringMonitoring,
        int pollIntervalMs = 10,
        CancellationToken cancellationToken = default);
}

