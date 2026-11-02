using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;

namespace EthioServe.Application.Payments;

public static partial class PaymentRules
{
    private const string ReferenceAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0, O, 1, I: easy to read out loud
    private const int ReferenceLength = 12;

    [GeneratedRegex("^[A-Za-z0-9_-]{8,64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    public static bool IsValidKey(string? key) => key is not null && KeyPattern().IsMatch(key);

    /// <summary>
    /// The key the database stores. It starts with the customer's id, so two customers can send the same text without
    /// colliding, and nobody can reuse or discover another customer's keys.
    /// </summary>
    public static string NamespacedKey(Guid customerId, string? key)
    {
        if (!IsValidKey(key))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(
                    "Idempotency-Key", "Send an Idempotency-Key header of 8 to 64 characters: letters, digits, '_' and '-'.")
            });
        }

        return $"{customerId:N}:{key}";
    }

    /// <summary>For example ES-20261102-K7M2QX9R4TBD: unique, unguessable and short enough to read to a support agent.</summary>
    public static string NewReference(DateTimeOffset now) =>
        $"ES-{now.UtcDateTime:yyyyMMdd}-{RandomNumberGenerator.GetString(ReferenceAlphabet, ReferenceLength)}";
}