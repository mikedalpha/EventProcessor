using EventProcessor.Model;
using EventProcessor.Persistence;

namespace EventProcessor.Tests;

public class InMemoryEventRepositoryTests
{
    private const string MessageId = "3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10";
    private const string OtherMessageId = "7b1f0c92-4d3a-4a1e-9f22-0c5d6e8a1b34";

    private static readonly DateTimeOffset ReceivedAt = new(2026, 5, 5, 22, 0, 0, TimeSpan.Zero);

    private static InMemoryEventRepository CreateRepository() => new();

    private static EventRecord EventFor(string messageId, string eventType = "ISSUED") =>
        new(messageId, eventType, ReceivedAt);

    [Fact]
    public async Task HasProcessedAsync_WhenNothingHasBeenSaved_ReturnsFalse()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var processed = await repository.HasProcessedAsync(MessageId, CancellationToken.None);

        // Assert
        Assert.False(processed);
    }

    [Fact]
    public async Task HasProcessedAsync_WhenThatMessageIdWasSaved_ReturnsTrue()
    {
        // Arrange
        var repository = CreateRepository();
        await repository.SaveAsync([EventFor(MessageId)], CancellationToken.None);

        // Act
        var processed = await repository.HasProcessedAsync(MessageId, CancellationToken.None);

        // Assert
        Assert.True(processed);
    }

    [Fact]
    public async Task HasProcessedAsync_WhenOnlyADifferentMessageIdWasSaved_ReturnsFalse()
    {
        // Arrange
        var repository = CreateRepository();
        await repository.SaveAsync([EventFor(OtherMessageId)], CancellationToken.None);

        // Act
        var processed = await repository.HasProcessedAsync(MessageId, CancellationToken.None);

        // Assert
        Assert.False(processed);
    }

    [Fact]
    public async Task HasProcessedAsync_WhenTwoMessageIdsWereSaved_ReportsBothIndependently()
    {
        // Arrange
        var repository = CreateRepository();
        await repository.SaveAsync([EventFor(MessageId)], CancellationToken.None);
        await repository.SaveAsync([EventFor(OtherMessageId)], CancellationToken.None);

        // Act
        var first = await repository.HasProcessedAsync(MessageId, CancellationToken.None);
        var second = await repository.HasProcessedAsync(OtherMessageId, CancellationToken.None);
        var unknown = await repository.HasProcessedAsync("not-a-message-id", CancellationToken.None);

        // Assert
        Assert.True(first);
        Assert.True(second);
        Assert.False(unknown);
    }

    [Fact]
    public async Task HasProcessedAsync_WhenAMessageCarriedSeveralEvents_StillReportsProcessedOnce()
    {
        // Arrange
        var repository = CreateRepository();
        await repository.SaveAsync(
            [EventFor(MessageId, "ISSUED"), EventFor(MessageId, "REPRICED")],
            CancellationToken.None);

        // Act
        var processed = await repository.HasProcessedAsync(MessageId, CancellationToken.None);

        // Assert
        Assert.True(processed);
    }
}
