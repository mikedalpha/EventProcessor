using System.Collections.Concurrent;
using EventProcessor.Abstractions;
using EventProcessor.Model;

namespace EventProcessor.Persistence;

public sealed class InMemoryEventRepository : IEventRepository
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<EventRecord>> _saved = new();

    public Task<bool> HasProcessedAsync(string messageId, CancellationToken cancellationToken) =>
        Task.FromResult(_saved.ContainsKey(messageId));

    public Task SaveAsync(IReadOnlyCollection<EventRecord> records, CancellationToken cancellationToken)
    {
        foreach (var message in records.GroupBy(record => record.MessageId))
        {
            _saved[message.Key] = new ConcurrentBag<EventRecord>(message);
        }

        return Task.CompletedTask;
    }
}
