using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using EventProcessor.Sources;

namespace EventProcessor.Tests;

public sealed class FileMessageSourceTests : IDisposable
{
    private const string Message =
        """<InstrumentMessage xmlns="urn:eventprocessor:instrument-message:v1" />""";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"source-{Guid.NewGuid():N}");

    public FileMessageSourceTests() => Directory.CreateDirectory(_directory);

    private static FileMessageSource CreateSource(string path) => new(
        Options.Create(new FileSourceOptions { Path = path }),
        NullLogger<FileMessageSource>.Instance);

    private static async Task<List<string>> ReadAllAsync(FileMessageSource source)
    {
        var contents = new List<string>();

        await foreach (var stream in source.ReadAsync(CancellationToken.None))
        {
            await using (stream)
            {
                using var reader = new StreamReader(stream);
                contents.Add(await reader.ReadToEndAsync(CancellationToken.None));
            }
        }

        return contents;
    }

    [Fact]
    public async Task ReadAsync_WhenTheFileExists_YieldsExactlyItsContent()
    {
        // Arrange
        var path = Path.Combine(_directory, "sample-message.xml");
        await File.WriteAllTextAsync(path, Message, CancellationToken.None);

        // Act
        var contents = await ReadAllAsync(CreateSource(path));

        // Assert
        Assert.Equal(new[] { Message }, contents.ToArray());
    }

    [Fact]
    public async Task ReadAsync_WhenTheFileIsMissing_YieldsNothing()
    {
        // Arrange
        var path = Path.Combine(_directory, "DoesNotExist.xml");

        // Act
        var contents = await ReadAllAsync(CreateSource(path));

        // Assert
        Assert.Empty(contents);
    }

    // A relative path is resolved against the binary rather than the working directory,
    // which is the bug startup validation caught the first time the app really ran.
    [Fact]
    public async Task ReadAsync_WhenThePathIsRelative_ResolvesItAgainstTheBinary()
    {
        // Arrange
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "sample-message.xml");
        var expected = await File.ReadAllTextAsync(path, CancellationToken.None);

        // Act
        var contents = await ReadAllAsync(CreateSource("Samples/sample-message.xml"));

        // Assert
        Assert.Equal(new[] { expected }, contents.ToArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
