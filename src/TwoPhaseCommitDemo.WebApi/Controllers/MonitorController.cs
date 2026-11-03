using Microsoft.AspNetCore.Mvc;
using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.WebApi.Common;

namespace TwoPhaseCommitDemo.WebApi.Controllers;

[Route("api/[controller]")]
public class MonitorController : ApiControllerBase
{
    private readonly IConsistencyMonitorService _monitorService;

    public MonitorController(IConsistencyMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    /// <summary>
    /// Retorna o status atual do serviço de monitoramento periódico.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            isRunning = _monitorService.IsRunning,
            pollingInterval = "10ms",
            message = _monitorService.IsRunning
                ? "Monitoramento ativo consultando PostgreSQL e MongoDB a cada 10ms."
                : "Monitoramento em background em pausa."
        });
    }

    /// <summary>
    /// Ativa o monitoramento contínuo em background (polling a cada 10ms).
    /// </summary>
    [HttpPost("start")]
    public IActionResult StartMonitoring()
    {
        _monitorService.StartMonitoring();
        return Ok(new { success = true, message = "Monitoramento a cada 10ms iniciado com sucesso." });
    }

    /// <summary>
    /// Pausa o monitoramento contínuo em background.
    /// </summary>
    [HttpPost("stop")]
    public IActionResult StopMonitoring()
    {
        _monitorService.StopMonitoring();
        return Ok(new { success = true, message = "Monitoramento pausado com sucesso." });
    }

    /// <summary>
    /// Retorna as leituras recentes coletadas a cada 10ms pelo serviço de monitoramento.
    /// </summary>
    [HttpGet("snapshots")]
    [ProducesResponseType(typeof(IReadOnlyList<MonitoringSnapshotDto>), StatusCodes.Status200OK)]
    public IActionResult GetSnapshots([FromQuery] int limit = 50)
    {
        var snapshots = _monitorService.GetRecentSnapshots(limit);
        return Ok(snapshots);
    }
}

