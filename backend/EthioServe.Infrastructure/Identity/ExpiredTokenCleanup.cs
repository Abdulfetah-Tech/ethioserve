using EthioServe.Application.Abstractions;
using EthioServe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Infrastructure.Identity;

public sealed class ExpiredTokenCleanup(AppDbContext db, IClock clock)
{
    public async Task<(int RefreshTokens, int PasswordResetTokens)> RunOnceAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var refreshTokens = await db.RefreshTokens
            .Where(token => token.ExpiresAt <= now)
            .ExecuteDeleteAsync(ct);
        var passwordResetTokens = await db.PasswordResetTokens
            .Where(token => token.ExpiresAt <= now || token.UsedAt != null)
            .ExecuteDeleteAsync(ct);

        return (refreshTokens, passwordResetTokens);
    }
}
