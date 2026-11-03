using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Domain.Common;

namespace TwoPhaseCommitDemo.Application.Interfaces;

public interface IProductService
{
    Task<Result<DatabaseStateSummaryDto>> GetCurrentStateAsync(CancellationToken cancellationToken = default);
    Task<Result<ProductResponseDto>> CreateProductTwoPhaseCommitAsync(CreateProductDto dto, CancellationToken cancellationToken = default);
    Task<Result> ClearDatabasesAsync(CancellationToken cancellationToken = default);
}

