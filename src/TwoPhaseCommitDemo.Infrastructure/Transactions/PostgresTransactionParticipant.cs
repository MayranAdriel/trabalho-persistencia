using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TwoPhaseCommitDemo.Domain.Common;
using TwoPhaseCommitDemo.Domain.Interfaces;
using TwoPhaseCommitDemo.Infrastructure.Data;

namespace TwoPhaseCommitDemo.Infrastructure.Transactions;

public class PostgresTransactionParticipant : ITransactionParticipant
{
    private readonly PostgresDbContext _dbContext;
    private readonly IDbContextTransaction _transaction;
    private readonly Func<PostgresDbContext, CancellationToken, Task> _operation;
    private readonly bool _simulateError;
    private readonly ILogger _logger;
    private bool _isPrepared;
    private bool _isFinished;

    public PostgresTransactionParticipant(
        PostgresDbContext dbContext,
        IDbContextTransaction transaction,
        Func<PostgresDbContext, CancellationToken, Task> operation,
        bool simulateError,
        ILogger logger)
    {
        _dbContext = dbContext;
        _transaction = transaction;
        _operation = operation;
        _simulateError = simulateError;
        _logger = logger;
    }

    public string Name => "PostgreSQL";

    public async Task<Result> PrepareAsync(CancellationToken cancellationToken = default)
    {
        if (_simulateError)
        {
            _logger.LogWarning("[PostgreSQL Participant] Simulação de erro ativada no Prepare! Falha intencional induzida.");
            return Result.Failure(Error.Database("POSTGRES_SIMULATED_ERROR", "Erro simulado induzido ao persistir dados no PostgreSQL."));
        }

        try
        {
            _logger.LogInformation("[PostgreSQL Participant] Executando operação e preparando transação...");
            await _operation(_dbContext, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _isPrepared = true;
            _logger.LogInformation("[PostgreSQL Participant] Fase 1 (Prepare) concluída com sucesso. Mudanças em buffer transacional.");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PostgreSQL Participant] Erro durante a fase Prepare.");
            return Result.Failure(Error.Database("POSTGRES_PREPARE_EXCEPTION", ex.Message));
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
            _logger.LogInformation("[PostgreSQL Participant] Executando Fase 2 (Commit) na transação...");
            await _transaction.CommitAsync(cancellationToken);
            _isFinished = true;
            _logger.LogInformation("[PostgreSQL Participant] Transação comitada com sucesso no PostgreSQL.");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PostgreSQL Participant] Falha ao comitar transação no PostgreSQL.");
            return Result.Failure(Error.Database("POSTGRES_COMMIT_EXCEPTION", ex.Message));
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
            _logger.LogWarning("[PostgreSQL Participant] Executando Rollback da transação no PostgreSQL...");
            await _transaction.RollbackAsync(cancellationToken);
            _isFinished = true;
            _logger.LogInformation("[PostgreSQL Participant] Rollback executado com sucesso no PostgreSQL. Alterações descartadas.");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PostgreSQL Participant] Erro durante o Rollback no PostgreSQL.");
            return Result.Failure(Error.Database("POSTGRES_ROLLBACK_EXCEPTION", ex.Message));
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _transaction.DisposeAsync();
            await _dbContext.DisposeAsync();
        }
        catch
        {
            // Ignored on cleanup
        }
    }
}
