using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EventProcessor.Abstractions;

namespace EventProcessor.Sources;

public sealed class FileMessageSource(
    IOptions<FileSourceOptions> options,
    ILogger<FileMessageSource> logger) : IMessageSource
{
    public IAsyncEnumerable<Stream> ReadAsync(CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(options.Value.Path, AppContext.BaseDirectory);

        if (!File.Exists(path))
        {
            logger.LogError("No message found at {Path}", path);
            return AsyncEnumerable.Empty<Stream>();
        }

        logger.LogInformation("Reading message from {Path}", path);

        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return new[] { stream }.ToAsyncEnumerable();
    }
}
