using EthioServe.Domain.Entities;
using FluentValidation;

namespace EthioServe.Application.Messages;

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(RequestMessage.MaxBodyLength);
    }
}
