using EthioServe.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EthioServe.Infrastructure.Identity;

/// <summary>Placeholder until a real provider (SMTP, SendGrid, ...) is chosen. It sends nothing: it only logs.</summary>
public sealed class DevelopmentEmailService(
    ILogger<DevelopmentEmailService> logger, IOptions<AppOptions> options) : IEmailService
{
    public bool IsAvailable => options.Value.LogEmailBodies;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (options.Value.LogEmailBodies)
        {
            logger.LogInformation("DEV EMAIL to {To} | {Subject}{NewLine}{Body}",
                message.To, message.Subject, Environment.NewLine, message.TextBody);
        }
        else
        {
            logger.LogInformation("Email to {To} ({Subject}) was not sent: no email provider is configured.",
                message.To, message.Subject);
        }

        return Task.CompletedTask;
    }
}