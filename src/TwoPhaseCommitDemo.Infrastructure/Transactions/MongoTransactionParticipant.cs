using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using TwoPhaseCommitDemo.Domain.Common;
using TwoPhaseCommitDemo.Domain.Interfaces;
using TwoPhaseCommitDemo.Infrastructure.Data;

namespace TwoPhaseCommitDemo.Infrastructure.Transactions;

public class MongoTransactionParticipant : ITransactionParticipant
{
    private readonly MongoDbContext _mongoDbContext;
    private readonly IClientSessionHandle _session;
    private readonly Func<IClientSessionHandle, MongoDbContext, CancellationToken, Task> _operation;
    private readonly bool _simulateError;
    private readonly ILogger _logger;
    private bool _isPrepared;
    private bool _isFinished;

    public MongoTransactionParticipant(
        MongoDbContext mongoDbContext,
        IClientSessionHandle session,
        Func<IClientSessionHandle, MongoDbContext, CancellationToken, Task> operation,
        bool simulateError,
        ILogger logger)
    {
        _mongoDbContext = mongoDbContext;
        _session = session;
        _operation = operation;
        _simulateError = simulateError;
        _logger = logger;
    }

    public string Name => "MongoDB";

    public async Task<Result> PrepareAsync(CancellationToken cancellationToken = default)
    {
        if (_simulateError)
        {
            _logger.LogWarning("[MongoDB Participant] Simulação de erro ativada no Prepare! Falha intencional induzida.");
            return Result.Failure(Error.Database("MONGO_SIMULATED_ERROR", "Erro simulado induzido ao salvar documento no MongoDB."));
        }

        try
        {
            _logger.LogInformation("[MongoDB Participant] Executando operação dentro da sessão com transação...");
            await _operation(_session, _mongoDbContext, cancellationToken);
            _isPrepared = true;
            _logger.LogInformation("[MongoDB Participant] Fase 1 (Prepare) concluída com sucesso. Documentos em buffer transacional.");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MongoDB Participant] Erro durante a fase Prepare.");
            return Result.Failure(Error.Database("MONGO_PREPARE_EXCEPTION", ex.Message));
        }
    }

    public async Task<Result> CommitAsync(CancellationToken cancellationToken = default)
    {
        if (!_isPrepared || _isFinished)
        {
            return Result.Success();
        }

        try
        {
            _logger.LogInformation("[MongoDB Participant] Executando Fase 2 (Commit) na transação do MongoDB...");
            if (_session.IsInTransaction)
            {
                await _session.CommitTransactionAsync(cancellationToken);
            }
            _isFinished = true;
            _logger.LogInformation("[MongoDB Participant] Transação comitada com sucesso no MongoDB.");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MongoDB Participant] Falha ao comitar transação no MongoDB.");
            return Result.Failure(Error.Database("MONGO_COMMIT_EXCEPTION", ex.Message));
        }
    }

    public async Task<Result> RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_isFinished)
        {
            return Result.Success();
        }

        try
        {
            _logger.LogWarning("[MongoDB Participant] Executando Abort/Rollback da transação no MongoDB...");
            if (_session.IsInTransaction)
            {
                await _session.AbortTransactionAsync(cancellationToken);
            }
            _isFinished = true;
            _logger.LogInformation("[MongoDB Participant] Rollback (Abort) executado com sucesso no MongoDB.");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MongoDB Participant] Erro durante o Rollback no MongoDB.");
            return Result.Failure(Error.Database("MONGO_ROLLBACK_EXCEPTION", ex.Message));
        }
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _session.Dispose();
        }
        catch
        {
            // Ignored on cleanup
        }
        return ValueTask.CompletedTask;
    }
}
