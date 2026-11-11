using Microsoft.Extensions.Logging;
using EventProcessor.Abstractions;

namespace EventProcessor.Email;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Mail to {Recipient}, subject {Subject}{NewLine}{Body}",
            recipient,
            subject,
            Environment.NewLine,
            body);

        return Task.CompletedTask;
    }
}
