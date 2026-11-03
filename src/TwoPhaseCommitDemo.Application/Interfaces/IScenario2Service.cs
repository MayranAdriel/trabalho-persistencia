using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Domain.Common;

namespace TwoPhaseCommitDemo.Application.Interfaces;

public interface IScenario2Service
{
    /// <summary>
    /// Cenário 2: Inserir os dados em uma tabela no MongoDB e depois no Postgres,
    /// porém, ao tentar salvar no Postgres, gera-se um erro e é necessário fazer "rollback" no MongoDB.
    /// </summary>
    Task<Result<ScenarioResultDto>> ExecuteAsync(CreateProductDto? dto = null, CancellationToken cancellationToken = default);
}

