namespace EthioServe.Application.Abstractions;

public sealed record EmailMessage(string To, string Subject, string TextBody);

/// <summary>Providers (SMTP, SendGrid, ...) implement this. Business code never depends on one of them.</summary>
public interface IEmailService
{
    bool IsAvailable { get; }

    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}