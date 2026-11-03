using Microsoft.Extensions.Logging;
using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.Domain.Common;
using TwoPhaseCommitDemo.Domain.Entities;
using TwoPhaseCommitDemo.Domain.Interfaces;

namespace TwoPhaseCommitDemo.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductPostgresRepository _postgresRepository;
    private readonly IProductMongoRepository _mongoRepository;
    private readonly ITwoPhaseCommitCoordinator _coordinator;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IProductPostgresRepository postgresRepository,
        IProductMongoRepository mongoRepository,
        ITwoPhaseCommitCoordinator coordinator,
        ILogger<ProductService> logger)
    {
        _postgresRepository = postgresRepository;
        _mongoRepository = mongoRepository;
        _coordinator = coordinator;
        _logger = logger;
    }

    public async Task<Result<DatabaseStateSummaryDto>> GetCurrentStateAsync(CancellationToken cancellationToken = default)
    {
        var pgProducts = await _postgresRepository.GetAllAsync(cancellationToken);
        var mongoProducts = await _mongoRepository.GetAllAsync(cancellationToken);

        var pgDtos = pgProducts.Select(MapToDto).ToList();
        var mongoDtos = mongoProducts.Select(MapToDto).ToList();

        var summary = new DatabaseStateSummaryDto(
            PostgresCount: pgDtos.Count,
            MongoCount: mongoDtos.Count,
            PostgresProducts: pgDtos,
            MongoProducts: mongoDtos
        );

        return Result.Success(summary);
    }

    public async Task<Result<ProductResponseDto>> CreateProductTwoPhaseCommitAsync(
        CreateProductDto dto,
        CancellationToken cancellationToken = default)
    {
        var product = Product.Create(dto.Sku, dto.Name, dto.Price, dto.StockQuantity);

        var pgParticipant = await _postgresRepository.CreateInsertParticipantAsync(product, simulateError: false, cancellationToken);
        var mongoParticipant = await _mongoRepository.CreateInsertParticipantAsync(product, simulateError: false, cancellationToken);

        var twoPhaseResult = await _coordinator.ExecuteTransactionAsync([pgParticipant, mongoParticipant], cancellationToken);

        if (twoPhaseResult.IsFailure)
        {
            return Result.Failure<ProductResponseDto>(twoPhaseResult.Error);
        }

        return Result.Success(MapToDto(product));
    }

    public async Task<Result> ClearDatabasesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _postgresRepository.ClearAllAsync(cancellationToken);
            await _mongoRepository.ClearAllAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear databases");
            return Result.Failure(Error.Database("CLEAR_DATABASES_ERROR", ex.Message));
        }
    }

    private static ProductResponseDto MapToDto(Product p) =>
        new(p.Id, p.Sku, p.Name, p.Price, p.StockQuantity, p.Version, p.CreatedAtUtc, p.UpdatedAtUtc);
}
