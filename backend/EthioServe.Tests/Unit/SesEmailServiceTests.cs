using System.Reflection;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using EthioServe.Application.Abstractions;
using EthioServe.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace EthioServe.Tests.Unit;

public sealed class SesEmailServiceTests
{
    [Fact]
    public async Task Sends_plain_text_email_from_the_configured_sender()
    {
        var client = DispatchProxy.Create<IAmazonSimpleEmailServiceV2, CapturingSesClient>();
        var capture = (CapturingSesClient)(object)client;
        var service = new SesEmailService(
            client,
            Options.Create(new EmailOptions { SenderAddress = "no-reply@example.com" }));

        Assert.True(service.IsAvailable);
        await service.SendAsync(new EmailMessage(
            "customer@example.net",
            "Reset your password",
            "Use this link to reset your password."));

        var request = Assert.IsType<SendEmailRequest>(capture.Request);
        Assert.Equal("no-reply@example.com", request.FromEmailAddress);
        Assert.Equal("customer@example.net", Assert.Single(request.Destination.ToAddresses));
        Assert.Equal("Reset your password", request.Content.Simple.Subject.Data);
        Assert.Equal("Use this link to reset your password.", request.Content.Simple.Body.Text.Data);
    }

    public class CapturingSesClient : DispatchProxy
    {
        public SendEmailRequest? Request { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IAmazonSimpleEmailServiceV2.SendEmailAsync)
                || args is not { Length: > 0 }
                || args[0] is not SendEmailRequest request)
            {
                throw new NotSupportedException($"Unexpected SES operation: {targetMethod?.Name ?? "(unknown)"}.");
            }

            Request = request;
            return Task.FromResult(new SendEmailResponse());
        }
    }
}
