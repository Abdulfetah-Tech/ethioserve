using EthioServe.API.Configuration;
using EthioServe.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace EthioServe.API.Services;

public sealed class ExpiredTokenCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<TokenCleanupSettings> settings,
    ILogger<ExpiredTokenCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.Value.TokenCleanupIntervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<ExpiredTokenCleanup>();
                var deleted = await cleanup.RunOnceAsync(stoppingToken);

                if (deleted.RefreshTokens + deleted.PasswordResetTokens > 0)
                {
                    logger.LogInformation(
                        "Removed {RefreshTokenCount} expired refresh tokens and {PasswordResetTokenCount} expired or used password reset tokens.",
                        deleted.RefreshTokens,
                        deleted.PasswordResetTokens);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Expired authentication token cleanup failed; it will retry on the next scheduled run.");
            }
        }
    }
}
