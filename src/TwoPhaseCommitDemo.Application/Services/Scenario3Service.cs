using Microsoft.Extensions.Logging;
using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.Domain.Common;
using TwoPhaseCommitDemo.Domain.Entities;
using TwoPhaseCommitDemo.Domain.Interfaces;

namespace TwoPhaseCommitDemo.Application.Services;

public class Scenario3Service : IScenario3Service
{
    private readonly IProductPostgresRepository _postgresRepository;
    private readonly IProductMongoRepository _mongoRepository;
    private readonly ITwoPhaseCommitCoordinator _coordinator;
    private readonly IProductService _productService;
    private readonly IConsistencyMonitorService _monitorService;
    private readonly ILogger<Scenario3Service> _logger;

    public Scenario3Service(
        IProductPostgresRepository postgresRepository,
        IProductMongoRepository mongoRepository,
        ITwoPhaseCommitCoordinator coordinator,
        IProductService productService,
        IConsistencyMonitorService monitorService,
        ILogger<Scenario3Service> logger)
    {
        _postgresRepository = postgresRepository;
        _mongoRepository = mongoRepository;
        _coordinator = coordinator;
        _productService = productService;
        _logger = logger;
        _monitorService = monitorService;
    }

    public async Task<Result<Scenario3ExecutionResultDto>> ExecuteAsync(
        int numberOfUpdates = 5,
        int delayBetweenUpdatesMs = 150,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("--- Iniciando Cenário 3: Atualizações concorrentes com monitoramento a cada 10ms ---");

        // 1. Garantir que temos um produto em ambos os bancos para atualizar
        var beforeStateResult = await _productService.GetCurrentStateAsync(cancellationToken);
        var beforeState = beforeStateResult.Value;

        Product targetProduct;
        if (beforeState.PostgresProducts.Count == 0 || beforeState.MongoProducts.Count == 0)
        {
            var createDto = new CreateProductDto(
                Sku: $"SC3-ITEM-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                Name: "Monitor Gamer 144Hz (Cenário 3 Base)",
                Price: 1200.00m,
                StockQuantity: 100
            );

            var createResult = await _productService.CreateProductTwoPhaseCommitAsync(createDto, cancellationToken);
            if (createResult.IsFailure)
            {
                return Result.Failure<Scenario3ExecutionResultDto>(createResult.Error);
            }

            var refreshedState = await _productService.GetCurrentStateAsync(cancellationToken);
            beforeState = refreshedState.Value;
        }

        var pgProduct = await _postgresRepository.GetByIdAsync(beforeState.PostgresProducts.First().Id, cancellationToken);
        if (pgProduct is null)
        {
            return Result.Failure<Scenario3ExecutionResultDto>(Error.NotFound("PRODUCT_NOT_FOUND", "Produto alvo para execução do Cenário 3 não foi localizado."));
        }

        targetProduct = pgProduct;

        TwoPhaseCommitResult? lastTxResult = null;

        // 2. Executar janela de monitoramento a cada 10ms enquanto as atualizações ocorrem
        var readings = await _monitorService.CaptureWindowAsync(
            async () =>
            {
                for (var i = 1; i <= numberOfUpdates; i++)
                {
                    targetProduct.Update(
                        name: $"Monitor Gamer 144Hz - Atualizado Ciclo #{i}",
                        price: targetProduct.Price + 10.00m * i,
                        stockQuantity: targetProduct.StockQuantity - i
                    );

                    _logger.LogInformation("[Cenário 3] Executando atualização 2PC #{Index} para o produto {ProductId}...", i, targetProduct.Id);

                    var pgParticipant = await _postgresRepository.CreateUpdateParticipantAsync(
                        targetProduct,
                        simulateError: false,
                        cancellationToken
                    );

                    var mongoParticipant = await _mongoRepository.CreateUpdateParticipantAsync(
                        targetProduct,
                        simulateError: false,
                        cancellationToken
                    );

                    var txResult = await _coordinator.ExecuteTransactionAsync([pgParticipant, mongoParticipant], cancellationToken);
                    if (txResult.IsSuccess)
                    {
                        lastTxResult = txResult.Value;
                    }

                    if (delayBetweenUpdatesMs > 0)
                    {
                        await Task.Delay(delayBetweenUpdatesMs, cancellationToken);
                    }
                }
            },
            pollIntervalMs: 10,
            cancellationToken
        );

        var afterStateResult = await _productService.GetCurrentStateAsync(cancellationToken);
        var afterState = afterStateResult.Value;

        var message = $"Cenário 3 concluído com sucesso: {numberOfUpdates} atualizações realizadas via 2PC enquanto o serviço consultou ambos os bancos a cada 10ms. Total de leituras capturadas: {readings.Count}.";

        var scenarioResult = new ScenarioResultDto(
            ScenarioName: "Cenário 3: Atualizações Concorrentes 2PC com Monitoramento a cada 10ms",
            Success: true,
            Message: message,
            TransactionDetails: lastTxResult,
            BeforeExecution: beforeState,
            AfterExecution: afterState
        );

        return Result.Success(new Scenario3ExecutionResultDto(
            ScenarioResult: scenarioResult,
            ConcurrentReadingsSample: readings.Take(50).ToList()
        ));
    }
}

