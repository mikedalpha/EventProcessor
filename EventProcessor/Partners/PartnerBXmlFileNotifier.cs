using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EventProcessor.Abstractions;
using EventProcessor.Model;

namespace EventProcessor.Partners;

public sealed class PartnerBXmlFileNotifier(
    IOptions<PartnerBOptions> options,
    ILogger<PartnerBXmlFileNotifier> logger) : IPartnerNotifier
{
    public string PartnerName => "Partner B";

    public bool ShouldHandle(InstrumentMessage message, InstrumentEvent instrumentEvent) =>
        instrumentEvent.EventType == options.Value.EventType;

    public async Task NotifyAsync(InstrumentMessage message, InstrumentEvent instrumentEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.Isin))
        {
            logger.LogWarning(
                "{Partner} notification skipped for {MessageId}: ISIN is required and was not set",
                PartnerName,
                message.MessageId);

            return;
        }

        var timestamp = DateTimeOffset.UtcNow;

        var notification = new XElement(
            "InstrumentNotification",
            new XElement("Timespan", timestamp.ToString("O")),
            new XElement("ISIN", message.Isin));

        // Resolved against the binary rather than the working directory so output lands in
        // the same place wherever the app is launched from
        var directory = Path.GetFullPath(options.Value.OutputDirectory, AppContext.BaseDirectory);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(
            directory,
            $"InstrumentNotification_{message.Isin}_{timestamp:yyyyMMdd'T'HHmmssfff'Z'}.xml");

        await using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await notification.SaveAsync(stream, SaveOptions.None, cancellationToken);

        logger.LogInformation(
            "{Partner} notification written to {Path} for {MessageId}",
            PartnerName,
            path,
            message.MessageId);
    }
}
