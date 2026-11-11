using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using EventProcessor.Model;
using EventProcessor.Partners;

namespace EventProcessor.Tests;

public sealed class PartnerBXmlFileNotifierTests : IDisposable
{
    private readonly string _outputDirectory =
        Path.Combine(Path.GetTempPath(), $"partner-b-{Guid.NewGuid():N}");

    private PartnerBXmlFileNotifier CreateNotifier(string eventType = "ISSUED") => new(
        Options.Create(new PartnerBOptions { EventType = eventType, OutputDirectory = _outputDirectory }),
        NullLogger<PartnerBXmlFileNotifier>.Instance);

    private static InstrumentMessage MessageWith(string? isin) =>
        new("3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10",
            "8.00% p.a. Barrier Reverse Convertible on Example Corp, Sample Industries",
            "ReverseConvertible",
            isin,
            []);

    [Fact]
    public void ShouldHandle_WhenTheEventTypeIsNotTheConfiguredOne_ReturnsFalse()
    {
        // Arrange
        var notifier = CreateNotifier();

        // Act
        var handled = notifier.ShouldHandle(MessageWith("XX1234567890"), new InstrumentEvent("REPRICED"));

        // Assert
        Assert.False(handled);
    }

    [Fact]
    public void ShouldHandle_WhenTheEventTypeIsTheConfiguredOne_ReturnsTrue()
    {
        // Arrange
        var notifier = CreateNotifier();

        // Act
        var handled = notifier.ShouldHandle(MessageWith("XX1234567890"), new InstrumentEvent("ISSUED"));

        // Assert
        Assert.True(handled);
    }

    // The filter is configuration, so pointing it at a different code has to change the
    // outcome. A hardcoded code passes the two tests above but cannot pass this one.
    [Fact]
    public void ShouldHandle_WhenConfiguredForADifferentCode_FollowsTheConfigurationNotAHardcodedCode()
    {
        // Arrange
        var notifier = CreateNotifier(eventType: "REPRICED");

        // Act
        var handlesConfigured = notifier.ShouldHandle(MessageWith("XX1234567890"), new InstrumentEvent("REPRICED"));
        var handlesIssued = notifier.ShouldHandle(MessageWith("XX1234567890"), new InstrumentEvent("ISSUED"));

        // Assert
        Assert.True(handlesConfigured);
        Assert.False(handlesIssued);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NotifyAsync_WhenTheIsinIsNotSet_WritesNoFile(string? isin)
    {
        // Arrange
        var notifier = CreateNotifier();

        // Act
        await notifier.NotifyAsync(MessageWith(isin), new InstrumentEvent("ISSUED"), CancellationToken.None);

        // Assert
        Assert.False(Directory.Exists(_outputDirectory) && Directory.EnumerateFiles(_outputDirectory).Any());
    }

    [Fact]
    public async Task NotifyAsync_WhenBothValuesAreSet_WritesTheExpectedNotification()
    {
        // Arrange
        var notifier = CreateNotifier();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        await notifier.NotifyAsync(MessageWith("XX1234567890"), new InstrumentEvent("ISSUED"), CancellationToken.None);

        // Assert
        var file = Assert.Single(Directory.EnumerateFiles(_outputDirectory));
        var notification = XElement.Parse(await File.ReadAllTextAsync(file, CancellationToken.None));

        Assert.Equal("InstrumentNotification", notification.Name.LocalName);
        Assert.Equal("XX1234567890", (string?)notification.Element("ISIN"));

        var timespanText = (string?)notification.Element("Timespan");
        Assert.NotNull(timespanText);
        Assert.InRange(DateTimeOffset.Parse(timespanText), before, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task NotifyAsync_WhenTheIsinIsSet_NamesTheFileAfterIt()
    {
        // Arrange
        var notifier = CreateNotifier();

        // Act
        await notifier.NotifyAsync(MessageWith("XX1234567890"), new InstrumentEvent("ISSUED"), CancellationToken.None);

        // Assert
        var file = Path.GetFileName(Assert.Single(Directory.EnumerateFiles(_outputDirectory)));
        Assert.StartsWith("InstrumentNotification_XX1234567890_", file);
        Assert.EndsWith(".xml", file);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }
}
