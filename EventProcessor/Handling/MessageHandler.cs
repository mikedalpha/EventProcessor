using Microsoft.Extensions.Logging;
using EventProcessor.Abstractions;
using EventProcessor.Model;

namespace EventProcessor.Handling;

public sealed class MessageHandler(
    IEventRepository repository,
    IEnumerable<IPartnerNotifier> notifiers,
    ILogger<MessageHandler> logger)
{
    public async Task HandleAsync(InstrumentMessage message, CancellationToken cancellationToken)
    {
        // Put MessageId on every log line written below this point for traceability
        using var scope = logger.BeginScope("MessageId:{MessageId}", message.MessageId);

        if (await repository.HasProcessedAsync(message.MessageId, cancellationToken))
        {
            logger.LogInformation("Message {MessageId} already processed, skipping", message.MessageId);
            return;
        }

        if (message.Events.Count == 0)
        {
            logger.LogWarning("Message {MessageId} carries no events, nothing to record or forward", message.MessageId);
            return;
        }

        var receivedAt = DateTimeOffset.UtcNow;

        var records = message.Events
            .Select(instrumentEvent => new EventRecord(message.MessageId, instrumentEvent.EventType, receivedAt))
            .ToArray();

        await repository.SaveAsync(records, cancellationToken);

        foreach (var instrumentEvent in message.Events)
        {
            await NotifyPartnersAsync(message, instrumentEvent, cancellationToken);
        }
    }

    private async Task NotifyPartnersAsync(InstrumentMessage message, InstrumentEvent instrumentEvent, CancellationToken cancellationToken)
    {
        foreach (var notifier in notifiers)
        {
            if (!notifier.ShouldHandle(message, instrumentEvent))
            {
                continue;
            }

            try
            {
                await notifier.NotifyAsync(message, instrumentEvent, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutdown is not a partner failure and must not be logged as one.
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "{Partner} failed for {MessageId} on event {EventType}",
                    notifier.PartnerName,
                    message.MessageId,
                    instrumentEvent.EventType);
            }
        }
    }
}
