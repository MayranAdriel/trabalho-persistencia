using Microsoft.Extensions.Logging;
using TwoPhaseCommitDemo.Domain.Common;
using TwoPhaseCommitDemo.Domain.Enums;
using TwoPhaseCommitDemo.Domain.Interfaces;

namespace TwoPhaseCommitDemo.Infrastructure.Transactions;

public class TwoPhaseCommitCoordinator : ITwoPhaseCommitCoordinator
{
    private readonly ILogger<TwoPhaseCommitCoordinator> _logger;

    public TwoPhaseCommitCoordinator(ILogger<TwoPhaseCommitCoordinator> logger)
    {
        _logger = logger;
    }

    public async Task<Result<TwoPhaseCommitResult>> ExecuteTransactionAsync(
        IEnumerable<ITransactionParticipant> participants,
        CancellationToken cancellationToken = default)
    {
        var participantList = participants.ToList();
        var txResult = new TwoPhaseCommitResult
        {
            FinalState = TransactionState.Initialized,
            Success = false
        };

        _logger.LogInformation("=== [2PC Coordinator] Iniciando protocolo Two-Phase Commit com {Count} participantes ===", participantList.Count);

        var preparedParticipants = new List<ITransactionParticipant>();

        try
        {
            // ==========================================
            // FASE 1: PREPARE (Votação / Pré-validação)
            // ==========================================
            txResult.FinalState = TransactionState.Preparing;
            _logger.LogInformation("--- [2PC Coordinator] FASE 1: PREPARE INICIADA ---");

            foreach (var participant in participantList)
            {
                _logger.LogInformation("[2PC Coordinator] Solicitando PREPARE para participante: {Name}...", participant.Name);
                var prepareResult = await participant.PrepareAsync(cancellationToken);

                if (prepareResult.IsFailure)
                {
                    txResult.ExecutionLogs.Add(new ParticipantLog(
                        Participant: participant.Name,
                        Phase: "PREPARE",
                        Success: false,
                        Details: prepareResult.Error.Message,
                        TimestampUtc: DateTime.UtcNow
                    ));

                    _logger.LogWarning("[2PC Coordinator] Falha no PREPARE do participante {Name}: {Error}", participant.Name, prepareResult.Error.Message);

                    // Falha no Prepare -> abortar e fazer Rollback de quem já preparou (e do falho)
                    await AbortAndRollbackAsync(participantList, txResult, cancellationToken);
                    txResult.Message = $"Transação abortada: Participante {participant.Name} falhou no Prepare. Rollback coordenado executado.";
                    return Result.Success(txResult);
                }

                preparedParticipants.Add(participant);
                txResult.ExecutionLogs.Add(new ParticipantLog(
                    Participant: participant.Name,
                    Phase: "PREPARE",
                    Success: true,
                    Details: "Participante validou e preparou a transação com sucesso.",
                    TimestampUtc: DateTime.UtcNow
                ));
            }

            // ==========================================
            // FASE 2: COMMIT (Efetivação Coordenada)
            // ==========================================
            txResult.FinalState = TransactionState.Committing;
            _logger.LogInformation("--- [2PC Coordinator] FASE 2: COMMIT INICIADA (Todos prepararam com sucesso) ---");

            foreach (var participant in participantList)
            {
                _logger.LogInformation("[2PC Coordinator] Solicitando COMMIT para participante: {Name}...", participant.Name);
                var commitResult = await participant.CommitAsync(cancellationToken);

                txResult.ExecutionLogs.Add(new ParticipantLog(
                    Participant: participant.Name,
                    Phase: "COMMIT",
                    Success: commitResult.IsSuccess,
                    Details: commitResult.IsSuccess ? "Commit efetuado com sucesso." : commitResult.Error.Message,
                    TimestampUtc: DateTime.UtcNow
                ));

                if (commitResult.IsFailure)
                {
                    _logger.LogError("[2PC Coordinator] Falha crítica no COMMIT do participante {Name}: {Error}", participant.Name, commitResult.Error.Message);
                }
            }

            txResult.FinalState = TransactionState.Committed;
            txResult.Success = true;
            txResult.Message = "Transação 2PC confirmada com sucesso em todos os bancos.";
            _logger.LogInformation("=== [2PC Coordinator] Transação 2PC finalizada com SUCESSO! ===");
            return Result.Success(txResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[2PC Coordinator] Exceção inesperada durante coordenação 2PC. Executando Rollback de emergência.");
            await AbortAndRollbackAsync(participantList, txResult, cancellationToken);
            txResult.Message = $"Exceção inesperada: {ex.Message}. Rollback emergencial executado.";
            return Result.Success(txResult);
        }
        finally
        {
            foreach (var participant in participantList)
            {
                try
                {
                    await participant.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Erro ao descartar participante {Name}", participant.Name);
                }
            }
        }
    }

    private async Task AbortAndRollbackAsync(
        List<ITransactionParticipant> participants,
        TwoPhaseCommitResult txResult,
        CancellationToken cancellationToken)
    {
        txResult.FinalState = TransactionState.Aborting;
        _logger.LogWarning("--- [2PC Coordinator] FASE DE ROLLBACK / ABORT INICIADA ---");

        foreach (var participant in participants)
        {
            try
            {
                _logger.LogInformation("[2PC Coordinator] Solicitando ROLLBACK para participante: {Name}...", participant.Name);
                var rollbackResult = await participant.RollbackAsync(cancellationToken);

                txResult.ExecutionLogs.Add(new ParticipantLog(
                    Participant: participant.Name,
                    Phase: "ROLLBACK",
                    Success: rollbackResult.IsSuccess,
                    Details: rollbackResult.IsSuccess ? "Rollback efetuado com sucesso." : rollbackResult.Error.Message,
                    TimestampUtc: DateTime.UtcNow
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[2PC Coordinator] Erro ao executar rollback no participante {Name}", participant.Name);
                txResult.ExecutionLogs.Add(new ParticipantLog(
                    Participant: participant.Name,
                    Phase: "ROLLBACK",
                    Success: false,
                    Details: $"Exceção no rollback: {ex.Message}",
                    TimestampUtc: DateTime.UtcNow
                ));
            }
        }

        txResult.FinalState = TransactionState.Aborted;
        txResult.Success = false;
        _logger.LogWarning("=== [2PC Coordinator] Transação 2PC ABORTADA. Todos os bancos retornaram ao estado consistente anterior. ===");
    }
}

