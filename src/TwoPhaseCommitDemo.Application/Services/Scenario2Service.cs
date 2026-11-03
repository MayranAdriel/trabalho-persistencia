using Microsoft.Extensions.Logging;
using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.Domain.Common;
using TwoPhaseCommitDemo.Domain.Entities;
using TwoPhaseCommitDemo.Domain.Interfaces;

namespace TwoPhaseCommitDemo.Application.Services;

public class Scenario2Service : IScenario2Service
{
    private readonly IProductPostgresRepository _postgresRepository;
    private readonly IProductMongoRepository _mongoRepository;
    private readonly ITwoPhaseCommitCoordinator _coordinator;
    private readonly IProductService _productService;
    private readonly ILogger<Scenario2Service> _logger;

    public Scenario2Service(
        IProductPostgresRepository postgresRepository,
        IProductMongoRepository mongoRepository,
        ITwoPhaseCommitCoordinator coordinator,
        IProductService productService,
        ILogger<Scenario2Service> logger)
    {
        _postgresRepository = postgresRepository;
        _mongoRepository = mongoRepository;
        _coordinator = coordinator;
        _productService = productService;
        _logger = logger;
    }

    public async Task<Result<ScenarioResultDto>> ExecuteAsync(CreateProductDto? dto = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("--- Iniciando Cenário 2: Inserir MongoDB -> Erro no Postgres -> Rollback MongoDB ---");

        var beforeStateResult = await _productService.GetCurrentStateAsync(cancellationToken);
        var beforeState = beforeStateResult.Value;

        var product = Product.Create(
            dto?.Sku ?? $"SC2-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            dto?.Name ?? "Mouse Gamer Sem Fio (Cenário 2)",
            dto?.Price ?? 189.50m,
            dto?.StockQuantity ?? 30
        );

        // Ordem do Cenário 2: MongoDB primeiro (sem erro), depois Postgres (com erro forçado)
        var mongoParticipant = await _mongoRepository.CreateInsertParticipantAsync(
            product,
            simulateError: false,
            cancellationToken
        );

        var pgParticipant = await _postgresRepository.CreateInsertParticipantAsync(
            product,
            simulateError: true,
            cancellationToken
        );

        var coordinatorResult = await _coordinator.ExecuteTransactionAsync(
            [mongoParticipant, pgParticipant],
            cancellationToken
        );

        var afterStateResult = await _productService.GetCurrentStateAsync(cancellationToken);
        var afterState = afterStateResult.Value;

        var wasRollbackSuccessful = afterState.PostgresCount == beforeState.PostgresCount &&
                                    afterState.MongoCount == beforeState.MongoCount;

        var message = wasRollbackSuccessful
            ? "Cenário 2 executado com sucesso: O erro simulado no Postgres acionou o 2PC Rollback no MongoDB. Nenhum dado inconsistente foi persistido."
            : "Alerta: O estado final dos bancos divergiu do esperado após o rollback.";

        var scenarioResult = new ScenarioResultDto(
            ScenarioName: "Cenário 2: Inserção MongoDB -> Erro Postgres -> Rollback MongoDB",
            Success: wasRollbackSuccessful,
            Message: message,
            TransactionDetails: coordinatorResult.IsSuccess ? coordinatorResult.Value : null,
            BeforeExecution: beforeState,
            AfterExecution: afterState
        );

        _logger.LogInformation("Cenário 2 finalizado. Resultado: {Message}", message);

        return Result.Success(scenarioResult);
    }
}

