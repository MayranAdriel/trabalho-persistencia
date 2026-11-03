using Microsoft.AspNetCore.Mvc;
using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.WebApi.Common;

namespace TwoPhaseCommitDemo.WebApi.Controllers;

[Route("api/[controller]")]
public class ScenariosController : ApiControllerBase
{
    private readonly IScenario1Service _scenario1Service;
    private readonly IScenario2Service _scenario2Service;
    private readonly IScenario3Service _scenario3Service;

    public ScenariosController(
        IScenario1Service scenario1Service,
        IScenario2Service scenario2Service,
        IScenario3Service scenario3Service)
    {
        _scenario1Service = scenario1Service;
        _scenario2Service = scenario2Service;
        _scenario3Service = scenario3Service;
    }

    /// <summary>
    /// Cenário 1: Inserir dados no Postgres e depois no MongoDB,
    /// porém, ao tentar salvar no MongoDB, ocorre um erro e é feito rollback no Postgres.
    /// </summary>
    [HttpPost("scenario-1")]
    [ProducesResponseType(typeof(ScenarioResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExecuteScenario1(
        [FromBody] CreateProductDto? dto,
        CancellationToken cancellationToken)
    {
        var result = await _scenario1Service.ExecuteAsync(dto, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cenário 2: Inserir dados no MongoDB e depois no Postgres,
    /// porém, ao tentar salvar no Postgres, ocorre um erro e é feito rollback no MongoDB.
    /// </summary>
    [HttpPost("scenario-2")]
    [ProducesResponseType(typeof(ScenarioResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExecuteScenario2(
        [FromBody] CreateProductDto? dto,
        CancellationToken cancellationToken)
    {
        var result = await _scenario2Service.ExecuteAsync(dto, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cenário 3: Atualizar dados no MongoDB e no Postgres simultaneamente via 2PC
    /// enquanto um serviço consulta constantemente (a cada 10ms) o estado de ambos os bancos.
    /// </summary>
    [HttpPost("scenario-3")]
    [ProducesResponseType(typeof(Scenario3ExecutionResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExecuteScenario3(
        [FromQuery] int numberOfUpdates = 5,
        [FromQuery] int delayBetweenUpdatesMs = 150,
        CancellationToken cancellationToken = default)
    {
        var result = await _scenario3Service.ExecuteAsync(
            numberOfUpdates,
            delayBetweenUpdatesMs,
            cancellationToken
        );

        return HandleResult(result);
    }
}

