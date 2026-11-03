using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TwoPhaseCommitDemo.Application.DTOs;
using TwoPhaseCommitDemo.Application.Interfaces;
using TwoPhaseCommitDemo.Application.Services;
using TwoPhaseCommitDemo.Domain.Common;
using TwoPhaseCommitDemo.Domain.Entities;
using TwoPhaseCommitDemo.Domain.Enums;
using TwoPhaseCommitDemo.Domain.Interfaces;
using Xunit;

namespace TwoPhaseCommitDemo.Tests;

public class ScenarioServicesTests
{
    [Fact]
    public async Task Scenario1Service_ShouldExecuteRollbackOnPostgres_WhenMongoFails()
    {
        // Arrange
        var mockPgRepo = new Mock<IProductPostgresRepository>();
        var mockMongoRepo = new Mock<IProductMongoRepository>();
        var mockCoordinator = new Mock<ITwoPhaseCommitCoordinator>();
        var mockProductService = new Mock<IProductService>();

        var emptyState = new DatabaseStateSummaryDto(0, 0, [], []);
        mockProductService.Setup(s => s.GetCurrentStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(emptyState));

        var mockPgParticipant = new Mock<ITransactionParticipant>();
        var mockMongoParticipant = new Mock<ITransactionParticipant>();

        mockPgRepo.Setup(r => r.CreateInsertParticipantAsync(It.IsAny<Product>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPgParticipant.Object);

        mockMongoRepo.Setup(r => r.CreateInsertParticipantAsync(It.IsAny<Product>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockMongoParticipant.Object);

        var abortedResult = new TwoPhaseCommitResult
        {
            FinalState = TransactionState.Aborted,
            Success = false,
            Message = "Aborted by simulated Mongo error"
        };

        mockCoordinator.Setup(c => c.ExecuteTransactionAsync(It.IsAny<IEnumerable<ITransactionParticipant>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(abortedResult));

        var service = new Scenario1Service(
            mockPgRepo.Object,
            mockMongoRepo.Object,
            mockCoordinator.Object,
            mockProductService.Object,
            NullLogger<Scenario1Service>.Instance
        );

        // Act
        var result = await service.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue(); // Confirmação que o cenário foi executado e bancos continuam com rollback consistente
        result.Value.BeforeExecution.PostgresCount.Should().Be(result.Value.AfterExecution.PostgresCount);
        result.Value.BeforeExecution.MongoCount.Should().Be(result.Value.AfterExecution.MongoCount);

        mockPgRepo.Verify(r => r.CreateInsertParticipantAsync(It.IsAny<Product>(), false, It.IsAny<CancellationToken>()), Times.Once);
        mockMongoRepo.Verify(r => r.CreateInsertParticipantAsync(It.IsAny<Product>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Scenario2Service_ShouldExecuteRollbackOnMongo_WhenPostgresFails()
    {
        // Arrange
        var mockPgRepo = new Mock<IProductPostgresRepository>();
        var mockMongoRepo = new Mock<IProductMongoRepository>();
        var mockCoordinator = new Mock<ITwoPhaseCommitCoordinator>();
        var mockProductService = new Mock<IProductService>();

        var emptyState = new DatabaseStateSummaryDto(0, 0, [], []);
        mockProductService.Setup(s => s.GetCurrentStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(emptyState));

        var mockPgParticipant = new Mock<ITransactionParticipant>();
        var mockMongoParticipant = new Mock<ITransactionParticipant>();

        mockMongoRepo.Setup(r => r.CreateInsertParticipantAsync(It.IsAny<Product>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockMongoParticipant.Object);

        mockPgRepo.Setup(r => r.CreateInsertParticipantAsync(It.IsAny<Product>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPgParticipant.Object);

        var abortedResult = new TwoPhaseCommitResult
        {
            FinalState = TransactionState.Aborted,
            Success = false,
            Message = "Aborted by simulated Postgres error"
        };

        mockCoordinator.Setup(c => c.ExecuteTransactionAsync(It.IsAny<IEnumerable<ITransactionParticipant>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(abortedResult));

        var service = new Scenario2Service(
            mockPgRepo.Object,
            mockMongoRepo.Object,
            mockCoordinator.Object,
            mockProductService.Object,
            NullLogger<Scenario2Service>.Instance
        );

        // Act
        var result = await service.ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        result.Value.BeforeExecution.PostgresCount.Should().Be(result.Value.AfterExecution.PostgresCount);
        result.Value.BeforeExecution.MongoCount.Should().Be(result.Value.AfterExecution.MongoCount);

        mockMongoRepo.Verify(r => r.CreateInsertParticipantAsync(It.IsAny<Product>(), false, It.IsAny<CancellationToken>()), Times.Once);
        mockPgRepo.Verify(r => r.CreateInsertParticipantAsync(It.IsAny<Product>(), true, It.IsAny<CancellationToken>()), Times.Once);
    }
}

