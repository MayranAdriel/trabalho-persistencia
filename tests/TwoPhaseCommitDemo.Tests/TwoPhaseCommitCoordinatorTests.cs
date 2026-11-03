using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TwoPhaseCommitDemo.Domain.Common;
using TwoPhaseCommitDemo.Domain.Enums;
using TwoPhaseCommitDemo.Domain.Interfaces;
using TwoPhaseCommitDemo.Infrastructure.Transactions;
using Xunit;

namespace TwoPhaseCommitDemo.Tests;

public class TwoPhaseCommitCoordinatorTests
{
    private readonly TwoPhaseCommitCoordinator _coordinator;

    public TwoPhaseCommitCoordinatorTests()
    {
        _coordinator = new TwoPhaseCommitCoordinator(NullLogger<TwoPhaseCommitCoordinator>.Instance);
    }

    [Fact]
    public async Task ExecuteTransactionAsync_ShouldCommitAllParticipants_WhenAllPrepareSuccessfully()
    {
        // Arrange
        var pgParticipant = new Mock<ITransactionParticipant>();
        pgParticipant.Setup(p => p.Name).Returns("PostgreSQL");
        pgParticipant.Setup(p => p.PrepareAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        pgParticipant.Setup(p => p.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var mongoParticipant = new Mock<ITransactionParticipant>();
        mongoParticipant.Setup(p => p.Name).Returns("MongoDB");
        mongoParticipant.Setup(p => p.PrepareAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        mongoParticipant.Setup(p => p.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _coordinator.ExecuteTransactionAsync([pgParticipant.Object, mongoParticipant.Object]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        result.Value.FinalState.Should().Be(TransactionState.Committed);

        pgParticipant.Verify(p => p.PrepareAsync(It.IsAny<CancellationToken>()), Times.Once);
        mongoParticipant.Verify(p => p.PrepareAsync(It.IsAny<CancellationToken>()), Times.Once);
        pgParticipant.Verify(p => p.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        mongoParticipant.Verify(p => p.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

        pgParticipant.Verify(p => p.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        mongoParticipant.Verify(p => p.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteTransactionAsync_ShouldRollbackPostgres_WhenMongoFailsPrepare_Scenario1()
    {
        // Arrange (Cenário 1: Postgres OK, Mongo Falha)
        var pgParticipant = new Mock<ITransactionParticipant>();
        pgParticipant.Setup(p => p.Name).Returns("PostgreSQL");
        pgParticipant.Setup(p => p.PrepareAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        pgParticipant.Setup(p => p.RollbackAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var mongoParticipant = new Mock<ITransactionParticipant>();
        mongoParticipant.Setup(p => p.Name).Returns("MongoDB");
        mongoParticipant.Setup(p => p.PrepareAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Database("MONGO_ERR", "Falha intencional ao salvar no MongoDB")));
        mongoParticipant.Setup(p => p.RollbackAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _coordinator.ExecuteTransactionAsync([pgParticipant.Object, mongoParticipant.Object]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeFalse();
        result.Value.FinalState.Should().Be(TransactionState.Aborted);

        // O Postgres preparou, mas deve ter sido revertido via Rollback
        pgParticipant.Verify(p => p.PrepareAsync(It.IsAny<CancellationToken>()), Times.Once);
        pgParticipant.Verify(p => p.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        pgParticipant.Verify(p => p.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);

        // O Mongo falhou e foi revertido/abortado
        mongoParticipant.Verify(p => p.PrepareAsync(It.IsAny<CancellationToken>()), Times.Once);
        mongoParticipant.Verify(p => p.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        mongoParticipant.Verify(p => p.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteTransactionAsync_ShouldRollbackMongo_WhenPostgresFailsPrepare_Scenario2()
    {
        // Arrange (Cenário 2: Mongo OK, Postgres Falha)
        var mongoParticipant = new Mock<ITransactionParticipant>();
        mongoParticipant.Setup(p => p.Name).Returns("MongoDB");
        mongoParticipant.Setup(p => p.PrepareAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        mongoParticipant.Setup(p => p.RollbackAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var pgParticipant = new Mock<ITransactionParticipant>();
        pgParticipant.Setup(p => p.Name).Returns("PostgreSQL");
        pgParticipant.Setup(p => p.PrepareAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Database("PG_ERR", "Falha intencional ao salvar no Postgres")));
        pgParticipant.Setup(p => p.RollbackAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _coordinator.ExecuteTransactionAsync([mongoParticipant.Object, pgParticipant.Object]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeFalse();
        result.Value.FinalState.Should().Be(TransactionState.Aborted);

        // O Mongo preparou, mas deve ter sido revertido via Rollback/Abort
        mongoParticipant.Verify(p => p.PrepareAsync(It.IsAny<CancellationToken>()), Times.Once);
        mongoParticipant.Verify(p => p.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        mongoParticipant.Verify(p => p.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);

        // O Postgres falhou e foi revertido
        pgParticipant.Verify(p => p.PrepareAsync(It.IsAny<CancellationToken>()), Times.Once);
        pgParticipant.Verify(p => p.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        pgParticipant.Verify(p => p.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

