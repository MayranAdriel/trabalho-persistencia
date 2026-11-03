using Microsoft.AspNetCore.Mvc;
using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.WebApi.Common;

namespace TwoPhaseCommitDemo.WebApi.Controllers;

[Route("api/[controller]")]
public class ProductsController : ApiControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Consulta o estado atual e lista os produtos em ambos os bancos (PostgreSQL e MongoDB).
    /// </summary>
    [HttpGet("state")]
    [ProducesResponseType(typeof(DatabaseStateSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentState(CancellationToken cancellationToken)
    {
        var result = await _productService.GetCurrentStateAsync(cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cadastra um produto com sucesso em ambos os bancos utilizando Two-Phase Commit (2PC).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _productService.CreateProductTwoPhaseCommitAsync(dto, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Limpa os registros de teste de ambos os bancos para permitir novas execuções limpas.
    /// </summary>
    [HttpDelete("clear")]
    public async Task<IActionResult> ClearDatabases(CancellationToken cancellationToken)
    {
        var result = await _productService.ClearDatabasesAsync(cancellationToken);
        return HandleResult(result);
    }
}

