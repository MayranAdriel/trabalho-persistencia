using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.Domain.Interfaces;

namespace TwoPhaseCommitDemo.Infrastructure.Services;

public class ConsistencyMonitorService : BackgroundService, IConsistencyMonitorService
{
    private readonly IProductPostgresRepository _postgresRepository;
    private readonly IProductMongoRepository _mongoRepository;
    private readonly ILogger<ConsistencyMonitorService> _logger;
    private readonly ConcurrentQueue<MonitoringSnapshotDto> _recentSnapshots = new();
    private const int MaxSnapshotBuffer = 500;

    private long _readingCounter;
    private volatile bool _isActivelyPolling = false;

    public ConsistencyMonitorService(
        IProductPostgresRepository postgresRepository,
        IProductMongoRepository mongoRepository,
        ILogger<ConsistencyMonitorService> logger)
    {
        _postgresRepository = postgresRepository;
        _mongoRepository = mongoRepository;
        _logger = logger;
    }

    public bool IsRunning => _isActivelyPolling;

    public void StartMonitoring()
    {
        _isActivelyPolling = true;
        _logger.LogInformation("[Consistency Monitor] Monitoramento constante a cada 10ms ATIVADO.");
    }

    public void StopMonitoring()
    {
        _isActivelyPolling = false;
        _logger.LogInformation("[Consistency Monitor] Monitoramento constante a cada 10ms PAUSADO.");
    }

    public IReadOnlyList<MonitoringSnapshotDto> GetRecentSnapshots(int limit = 50)
    {
        return _recentSnapshots.Reverse().Take(limit).ToList();
    }

    public async Task<IReadOnlyList<MonitoringSnapshotDto>> CaptureWindowAsync(
        Func<Task> actionDuringMonitoring,
        int pollIntervalMs = 10,
        CancellationToken cancellationToken = default)
    {
        var windowSnapshots = new List<MonitoringSnapshotDto>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _logger.LogInformation("[Consistency Monitor] Iniciando janela de captura a cada {Interval}ms...", pollIntervalMs);

        // Dispara tarefa de amostragem constante a cada 10ms
        var pollingTask = Task.Run(async () =>
        {
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(pollIntervalMs));
            while (!cts.Token.IsCancellationRequested && await periodicTimer.WaitForNextTickAsync(cts.Token))
            {
                try
                {
                    var snapshot = await TakeSnapshotAsync(cts.Token);
                    lock (windowSnapshots)
                    {
                        windowSnapshots.Add(snapshot);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[Consistency Monitor] Exceção na leitura periódica de 10ms.");
                }
            }
        }, cts.Token);

        try
        {
            // Executa a ação (ex: atualizações 2PC nos bancos)
            await actionDuringMonitoring();

            // Aguarda um pequeno instante para capturar estabilização final
            await Task.Delay(50, cancellationToken);
        }
        finally
        {
            await cts.CancelAsync();
            try
            {
                await pollingTask;
            }
            catch (OperationCanceledException)
            {
                // Esperado no cancelamento
            }
        }

        _logger.LogInformation("[Consistency Monitor] Janela de captura concluída. {Count} leituras a cada {Interval}ms capturadas.", windowSnapshots.Count, pollIntervalMs);
        return windowSnapshots;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));

        while (!stoppingToken.IsCancellationRequested && await periodicTimer.WaitForNextTickAsync(stoppingToken))
        {
            if (!_isActivelyPolling)
            {
                continue;
            }

            try
            {
                var snapshot = await TakeSnapshotAsync(stoppingToken);
                _recentSnapshots.Enqueue(snapshot);

                while (_recentSnapshots.Count > MaxSnapshotBuffer)
                {
                    _recentSnapshots.TryDequeue(out _);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[Consistency Monitor] Erro no polling de 10ms em background.");
            }
        }
    }

    private async Task<MonitoringSnapshotDto> TakeSnapshotAsync(CancellationToken cancellationToken)
    {
        var currentIndex = Interlocked.Increment(ref _readingCounter);
        var pgTask = _postgresRepository.GetAllAsync(cancellationToken);
        var mongoTask = _mongoRepository.GetAllAsync(cancellationToken);

        await Task.WhenAll(pgTask, mongoTask);

        var pgList = await pgTask;
        var mongoList = await mongoTask;

        var isInSync = pgList.Count == mongoList.Count;
        var details = isInSync
            ? $"Sincronizado ({pgList.Count} itens em ambos)"
            : $"Transitório: PG tem {pgList.Count} itens vs Mongo tem {mongoList.Count} itens";

        if (isInSync && pgList.Count > 0)
        {
            // Checar se as versões e preços batem
            var pgFirst = pgList[0];
            var mongoFirst = mongoList.FirstOrDefault(m => m.Id == pgFirst.Id);
            if (mongoFirst != null)
            {
                var versionsMatch = pgFirst.Version == mongoFirst.Version;
                var pricesMatch = pgFirst.Price == mongoFirst.Price;

                if (!versionsMatch || !pricesMatch)
                {
                    isInSync = false;
                    details = $"Diferença de versão/preço em voo: PG(v{pgFirst.Version}, R${pgFirst.Price}) vs Mongo(v{mongoFirst.Version}, R${mongoFirst.Price})";
                }
                else
                {
                    details = $"Totalmente consistente: v{pgFirst.Version}, R${pgFirst.Price} em ambos os bancos.";
                }
            }
        }

        return new MonitoringSnapshotDto(
            ReadingIndex: currentIndex,
            TimestampUtc: DateTime.UtcNow,
            PostgresCount: pgList.Count,
            MongoCount: mongoList.Count,
            IsInSync: isInSync,
            Details: details
        );
    }
}

