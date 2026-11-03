using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Domain.Common;

namespace TwoPhaseCommitDemo.Application.Interfaces;

public interface IScenario3Service
{
    /// <summary>
    /// Cenário 3: Atualizar os dados no MongoDB e no Postgres enquanto um serviço
    /// consulta constantemente (a cada 10ms) a lista de dados de ambos os bancos.
    /// </summary>
    Task<Result<Scenario3ExecutionResultDto>> ExecuteAsync(
        int numberOfUpdates = 5,
        int delayBetweenUpdatesMs = 150,
        CancellationToken cancellationToken = default);
}

