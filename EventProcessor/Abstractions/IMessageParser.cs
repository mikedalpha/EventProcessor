using EventProcessor.Model;

namespace EventProcessor.Abstractions;

public interface IMessageParser
{
    Task<InstrumentMessage> ParseAsync(Stream stream, CancellationToken cancellationToken);
}
