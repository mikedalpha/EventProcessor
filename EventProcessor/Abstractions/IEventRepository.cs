using EventProcessor.Model;

namespace EventProcessor.Abstractions;

public interface IEventRepository
{
    Task<bool> HasProcessedAsync(string messageId, CancellationToken cancellationToken);

    Task SaveAsync(IReadOnlyCollection<EventRecord> records, CancellationToken cancellationToken);
}
