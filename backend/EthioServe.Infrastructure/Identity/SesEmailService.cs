using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using EthioServe.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace EthioServe.Infrastructure.Identity;

public sealed class SesEmailService(
    IAmazonSimpleEmailServiceV2 client,
    IOptions<EmailOptions> options) : IEmailService
{
    public bool IsAvailable => true;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var request = new SendEmailRequest
        {
            FromEmailAddress = options.Value.SenderAddress,
            Destination = new Destination { ToAddresses = [message.To] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = message.Subject, Charset = "UTF-8" },
                    Body = new Body
                    {
                        Text = new Content { Data = message.TextBody, Charset = "UTF-8" }
                    }
                }
            }
        };

        await client.SendEmailAsync(request, ct);
    }
}
