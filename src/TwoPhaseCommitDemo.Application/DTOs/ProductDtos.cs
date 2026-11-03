namespace TwoPhaseCommitDemo.Application.DTOs;

public record CreateProductDto(
    string Sku,
    string Name,
    decimal Price,
    int StockQuantity
);

public record UpdateProductDto(
    string Name,
    decimal Price,
    int StockQuantity
);

public record ProductResponseDto(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    int StockQuantity,
    int Version,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

