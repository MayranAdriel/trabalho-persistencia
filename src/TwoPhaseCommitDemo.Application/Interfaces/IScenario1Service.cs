using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Domain.Common;

namespace TwoPhaseCommitDemo.Application.Interfaces;

public interface IScenario1Service
{
    /// <summary>
    /// Cenário 1: Inserir os dados em uma tabela no Postgres e depois no MongoDB,
    /// porém, ao tentar salvar no MongoDB, gera-se um erro e é necessário fazer rollback no Postgres.
    /// </summary>
    Task<Result<ScenarioResultDto>> ExecuteAsync(CreateProductDto? dto = null, CancellationToken cancellationToken = default);
}

