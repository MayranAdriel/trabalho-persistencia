using Microsoft.Extensions.Logging;
using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.Domain.Common;
using TwoPhaseCommitDemo.Domain.Entities;
using TwoPhaseCommitDemo.Domain.Interfaces;

namespace TwoPhaseCommitDemo.Application.Services;

public class Scenario1Service : IScenario1Service
{
    private readonly IProductPostgresRepository _postgresRepository;
    private readonly IProductMongoRepository _mongoRepository;
    private readonly ITwoPhaseCommitCoordinator _coordinator;
    private readonly IProductService _productService;
    private readonly ILogger<Scenario1Service> _logger;

    public Scenario1Service(
        IProductPostgresRepository postgresRepository,
        IProductMongoRepository mongoRepository,
        ITwoPhaseCommitCoordinator coordinator,
        IProductService productService,
        ILogger<Scenario1Service> logger)
    {
        _postgresRepository = postgresRepository;
        _mongoRepository = mongoRepository;
        _coordinator = coordinator;
        _productService = productService;
        _logger = logger;
    }

    public async Task<Result<ScenarioResultDto>> ExecuteAsync(CreateProductDto? dto = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("--- Iniciando Cenário 1: Inserir Postgres -> Erro no MongoDB -> Rollback Postgres ---");

        var beforeStateResult = await _productService.GetCurrentStateAsync(cancellationToken);
        var beforeState = beforeStateResult.Value;

        var product = Product.Create(
            dto?.Sku ?? $"SC1-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            dto?.Name ?? "Teclado Mecânico RGB (Cenário 1)",
            dto?.Price ?? 299.90m,
            dto?.StockQuantity ?? 50
        );

        // Ordem do Cenário 1: Postgres primeiro (sem erro), depois MongoDB (com erro forçado)
        var pgParticipant = await _postgresRepository.CreateInsertParticipantAsync(
            product,
            simulateError: false,
            cancellationToken
        );

        var mongoParticipant = await _mongoRepository.CreateInsertParticipantAsync(
            product,
            simulateError: true,
            cancellationToken
        );

        var coordinatorResult = await _coordinator.ExecuteTransactionAsync(
            [pgParticipant, mongoParticipant],
            cancellationToken
        );

        var afterStateResult = await _productService.GetCurrentStateAsync(cancellationToken);
        var afterState = afterStateResult.Value;

        var wasRollbackSuccessful = afterState.PostgresCount == beforeState.PostgresCount &&
                                    afterState.MongoCount == beforeState.MongoCount;

        var message = wasRollbackSuccessful
            ? "Cenário 1 executado com sucesso: O erro simulado no MongoDB acionou o 2PC Rollback no Postgres. Nenhum dado inconsistente foi persistido."
            : "Alerta: O estado final dos bancos divergiu do esperado após o rollback.";

        var scenarioResult = new ScenarioResultDto(
            ScenarioName: "Cenário 1: Inserção Postgres -> Erro MongoDB -> Rollback Postgres",
            Success: wasRollbackSuccessful,
            Message: message,
            TransactionDetails: coordinatorResult.IsSuccess ? coordinatorResult.Value : null,
            BeforeExecution: beforeState,
            AfterExecution: afterState
        );

        _logger.LogInformation("Cenário 1 finalizado. Resultado: {Message}", message);

        return Result.Success(scenarioResult);
    }
}

