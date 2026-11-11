using EventProcessor.Model;

namespace EventProcessor.Abstractions;

public interface IPartnerNotifier
{
    string PartnerName { get; }

    bool ShouldHandle(InstrumentMessage message, InstrumentEvent instrumentEvent);

    Task NotifyAsync(InstrumentMessage message, InstrumentEvent instrumentEvent, CancellationToken cancellationToken);
}
