using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using EthioServe.Application.Abstractions;

namespace EthioServe.Tests.Support;

/// <summary>Captures outgoing emails so tests can read the reset link, the only place the raw token exists.</summary>
public sealed class TestEmailService : IEmailService
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public bool IsAvailable { get; set; } = true;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public EmailMessage? LastTo(string address) =>
        _sent.Reverse().FirstOrDefault(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase));

    public int CountTo(string address) =>
        _sent.Count(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase));

    public static string ExtractToken(EmailMessage message)
    {
        var match = Regex.Match(message.TextBody, @"token=(?<t>[A-Za-z0-9_\-]+)");

        return match.Success
            ? match.Groups["t"].Value
            : throw new InvalidOperationException("No reset token was found in the email.");
    }
}