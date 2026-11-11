using Microsoft.Extensions.Options;
using EventProcessor.Abstractions;
using EventProcessor.Model;

namespace EventProcessor.Partners;

public sealed class PartnerAEmailNotifier(
    IEmailSender emailSender,
    IOptions<PartnerAOptions> options) : IPartnerNotifier
{
    public string PartnerName => "Partner A";

    public bool ShouldHandle(InstrumentMessage message, InstrumentEvent instrumentEvent) => true;

    public Task NotifyAsync(InstrumentMessage message, InstrumentEvent instrumentEvent, CancellationToken cancellationToken)
    {
        var subject = $"Event {instrumentEvent.EventType} for {message.Isin}";

        var body = $"""
            Product name: {message.ProductName}
            Product type: {message.ProductType}
            Event type: {instrumentEvent.EventType}
            ISIN: {message.Isin}
            """;

        return emailSender.SendAsync(options.Value.Recipient, subject, body, cancellationToken);
    }
}
