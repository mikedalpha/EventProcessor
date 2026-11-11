namespace EventProcessor.Abstractions;

public interface IMessageSource
{
    IAsyncEnumerable<Stream> ReadAsync(CancellationToken cancellationToken);
}
