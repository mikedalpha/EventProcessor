using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using EventProcessor.Abstractions;
using EventProcessor.Handling;
using EventProcessor.Model;

namespace EventProcessor.Tests;

public class MessageHandlerTests
{
    private const string MessageId = "3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10";

    private static InstrumentMessage MessageWith(params string[] eventTypes) =>
        new(MessageId,
            "8.00% p.a. Barrier Reverse Convertible on Example Corp, Sample Industries",
            "ReverseConvertible",
            "XX1234567890",
            eventTypes.Select(t => new InstrumentEvent(t)).ToList());

    private static MessageHandler CreateHandler(
        IEventRepository repository,
        params IPartnerNotifier[] notifiers) =>
        new(repository, notifiers, NullLogger<MessageHandler>.Instance);

    private static IPartnerNotifier NotifierThat(bool shouldHandle, string name = "Test partner")
    {
        var notifier = Substitute.For<IPartnerNotifier>();
        notifier.PartnerName.Returns(name);
        notifier.ShouldHandle(Arg.Any<InstrumentMessage>(), Arg.Any<InstrumentEvent>()).Returns(shouldHandle);
        return notifier;
    }

    private static IReadOnlyCollection<EventRecord>[] SaveCalls(IEventRepository repository) =>
        repository.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IEventRepository.SaveAsync))
            .Select(call => (IReadOnlyCollection<EventRecord>)call.GetArguments()[0]!)
            .ToArray();

    private static EventRecord[] SavedRecords(IEventRepository repository) =>
        SaveCalls(repository).SelectMany(records => records).ToArray();

    [Fact]
    public async Task HandleAsync_WhenTheMessageIdWasAlreadyProcessed_SavesNothingAndNotifiesNobody()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        repository.HasProcessedAsync(MessageId, Arg.Any<CancellationToken>()).Returns(true);
        var notifier = NotifierThat(shouldHandle: true);

        // Act
        await CreateHandler(repository, notifier).HandleAsync(MessageWith("ISSUED"), CancellationToken.None);

        // Assert
        Assert.Empty(SavedRecords(repository));
        await notifier.DidNotReceive().NotifyAsync(Arg.Any<InstrumentMessage>(), Arg.Any<InstrumentEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenTheMessageHasNoEvents_SavesNothingAndNotifiesNobody()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        var notifier = NotifierThat(shouldHandle: true);

        // Act
        await CreateHandler(repository, notifier).HandleAsync(MessageWith(), CancellationToken.None);

        // Assert
        Assert.Empty(SavedRecords(repository));
        await notifier.DidNotReceive().NotifyAsync(Arg.Any<InstrumentMessage>(), Arg.Any<InstrumentEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenTheMessageCarriesSeveralEvents_SavesOneRowPerEvent()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();

        // Act
        await CreateHandler(repository).HandleAsync(MessageWith("ISSUED", "REPRICED"), CancellationToken.None);

        // Assert
        var saved = SavedRecords(repository);
        Assert.Equal(new[] { "ISSUED", "REPRICED" }, saved.Select(r => r.EventType).ToArray());
        Assert.All(saved, r => Assert.Equal(MessageId, r.MessageId));
    }

    [Fact]
    public async Task HandleAsync_WhenTheMessageCarriesSeveralEvents_SavesThemInASingleCall()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();

        // Act
        await CreateHandler(repository).HandleAsync(MessageWith("ISSUED", "REPRICED", "MATURED"), CancellationToken.None);

        // Assert
        var call = Assert.Single(SaveCalls(repository));
        Assert.Equal(3, call.Count);
    }

    [Fact]
    public async Task HandleAsync_WhenSavingFails_NotifiesNobody()
    {
        // Arrange
        var repository = Substitute.For<IEventRepository>();
        repository.SaveAsync(Arg.Any<IReadOnlyCollection<EventRecord>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var notifier = NotifierThat(shouldHandle: true);

        // Act
        var handle = async () => await CreateHandler(repository, notifier)
            .HandleAsync(MessageWith("ISSUED"), CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(handle);
        await notifier.DidNotReceive().NotifyAsync(Arg.Any<InstrumentMessage>(), Arg.Any<InstrumentEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAPartnerIsNotInterested_OnlyNotifiesTheOneThatIs()
    {
        // Arrange
        var interested = NotifierThat(shouldHandle: true, name: "Interested");
        var uninterested = NotifierThat(shouldHandle: false, name: "Uninterested");

        // Act
        await CreateHandler(Substitute.For<IEventRepository>(), interested, uninterested)
            .HandleAsync(MessageWith("ISSUED"), CancellationToken.None);

        // Assert
        await interested.Received(1).NotifyAsync(Arg.Any<InstrumentMessage>(), Arg.Any<InstrumentEvent>(), Arg.Any<CancellationToken>());
        await uninterested.DidNotReceive().NotifyAsync(Arg.Any<InstrumentMessage>(), Arg.Any<InstrumentEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenOnePartnerThrows_StillNotifiesTheOthers()
    {
        // Arrange
        var failing = NotifierThat(shouldHandle: true, name: "Partner A");
        failing.NotifyAsync(Arg.Any<InstrumentMessage>(), Arg.Any<InstrumentEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("mail server down"));
        var healthy = NotifierThat(shouldHandle: true, name: "Partner B");

        // Act
        await CreateHandler(Substitute.For<IEventRepository>(), failing, healthy)
            .HandleAsync(MessageWith("ISSUED"), CancellationToken.None);

        // Assert
        await healthy.Received(1).NotifyAsync(Arg.Any<InstrumentMessage>(), Arg.Any<InstrumentEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenTwoEventsMatch_NotifiesOncePerEvent()
    {
        // Arrange
        var notifier = NotifierThat(shouldHandle: true);

        // Act
        await CreateHandler(Substitute.For<IEventRepository>(), notifier)
            .HandleAsync(MessageWith("ISSUED", "ISSUED"), CancellationToken.None);

        // Assert
        await notifier.Received(2).NotifyAsync(Arg.Any<InstrumentMessage>(), Arg.Any<InstrumentEvent>(), Arg.Any<CancellationToken>());
    }
}
