using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EventProcessor.Abstractions;

namespace EventProcessor.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(settings.UserName, settings.Password),
        };

        using var mail = new MailMessage(settings.From, recipient, subject, body);

        await client.SendMailAsync(mail, cancellationToken);

        logger.LogInformation("Mail sent to {Recipient} via {Host}", recipient, settings.Host);
    }
}
