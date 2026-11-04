using EthioServe.Application.Auth;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Identity;
using EthioServe.Tests.Support;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public sealed class PasswordResetServiceTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string NewPassword = "An0therStr0ngPass!";

    private AuthHarness _auth = null!;

    public async Task InitializeAsync() => _auth = await AuthHarness.CreateAsync(fixture);

    public async Task DisposeAsync() => await _auth.DisposeAsync();

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@test.local";

    private Task<AuthTokens> RegisterAsync(string email) =>
        _auth.Call(a => a.RegisterCustomerAsync(
            new RegisterCustomerRequest(email, AuthHarness.StrongPassword, "Abebe", "Kebede", "0911223344"),
            AuthHarness.Client));

    private Task ForgotAsync(string email) =>
        _auth.CallVoid(a => a.RequestPasswordResetAsync(new ForgotPasswordRequest(email), AuthHarness.Client));

    private Task ResetAsync(string token, string password = NewPassword) =>
        _auth.CallVoid(a => a.ResetPasswordAsync(new ResetPasswordRequest(token, password)));

    private Task<AuthTokens> LoginAsync(string email, string password) =>
        _auth.Call(a => a.LoginAsync(new LoginRequest(email, password), AuthHarness.Client));

    private string LastTokenFor(string email) => TestEmailService.ExtractToken(_auth.Emails.LastTo(email)!);

    [Fact]
    public async Task Unknown_email_sends_nothing_and_does_not_fail()
    {
        var email = NewEmail();

        await ForgotAsync(email);

        Assert.Equal(0, _auth.Emails.CountTo(email));
    }

    [Fact]
    public async Task Known_email_gets_a_link_and_only_the_hash_is_stored()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        await ForgotAsync(email);

        var message = _auth.Emails.LastTo(email);
        Assert.NotNull(message);
        Assert.Contains("http://localhost:4200/reset-password?token=", message.TextBody);

        var token = TestEmailService.ExtractToken(message);
        var stored = await _auth.Db(db => db.PasswordResetTokens.Select(t => t.TokenHash).ToListAsync());
        Assert.DoesNotContain(token, stored);
        Assert.Contains(_auth.Hash(token), stored);
    }

    [Fact]
    public async Task Reset_changes_the_password()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        await ForgotAsync(email);

        await ResetAsync(LastTokenFor(email));

        await Assert.ThrowsAsync<UnauthorizedException>(() => LoginAsync(email, AuthHarness.StrongPassword));
        var login = await LoginAsync(email, NewPassword);
        Assert.Equal(email, login.User.Email);
    }

    [Fact]
    public async Task A_reset_link_can_only_be_used_once()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        await ForgotAsync(email);
        var token = LastTokenFor(email);

        await ResetAsync(token);

        await Assert.ThrowsAsync<ValidationException>(() => ResetAsync(token, "Y3tAnotherPassw0rd!"));
    }

    [Fact]
    public async Task Reset_signs_the_user_out_everywhere()
    {
        var email = NewEmail();
        var session = await RegisterAsync(email);
        await ForgotAsync(email);

        await ResetAsync(LastTokenFor(email));

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _auth.Call(a => a.RefreshAsync(session.RefreshToken, AuthHarness.Client)));
    }

    [Fact]
    public async Task Expired_link_is_rejected()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        await ForgotAsync(email);
        var token = LastTokenFor(email);

        _auth.Clock.UtcNow = _auth.Clock.UtcNow.AddMinutes(61);

        await Assert.ThrowsAsync<ValidationException>(() => ResetAsync(token));
    }

    [Fact]
    public async Task Requesting_a_new_link_invalidates_the_previous_one()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        await ForgotAsync(email);
        var first = LastTokenFor(email);
        await ForgotAsync(email);
        var second = LastTokenFor(email);

        Assert.NotEqual(first, second);
        await Assert.ThrowsAsync<ValidationException>(() => ResetAsync(first));
        await ResetAsync(second);
    }

    [Fact]
    public async Task A_weak_new_password_is_rejected_without_consuming_the_link()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        await ForgotAsync(email);
        var token = LastTokenFor(email);

        await Assert.ThrowsAsync<ValidationException>(() => ResetAsync(token, "weak"));

        await ResetAsync(token); // the link still works for a good password
    }

    [Fact]
    public async Task Unknown_token_is_rejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() => ResetAsync("not-a-real-token"));
    }

    [Fact]
    public async Task Suspended_user_gets_no_reset_email()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        await _auth.DbVoid(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Suspend();
            await db.SaveChangesAsync();
        });

        await ForgotAsync(email);

        Assert.Equal(0, _auth.Emails.CountTo(email));
    }

    [Fact]
    public async Task Reset_unlocks_a_locked_account()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => LoginAsync(email, "Wr0ngPassword!"));
        }

        await ForgotAsync(email);
        await ResetAsync(LastTokenFor(email));

        var login = await LoginAsync(email, NewPassword);
        Assert.Equal(email, login.User.Email);
    }

    [Fact]
    public async Task Expired_token_cleanup_removes_only_expired_or_used_tokens()
    {
        var email = NewEmail();
        var session = await RegisterAsync(email);
        var now = _auth.Clock.UtcNow;
        var expiredRefreshHash = $"expired-{Guid.NewGuid():N}";
        var activeRefreshHash = $"active-{Guid.NewGuid():N}";
        var expiredResetHash = $"expired-{Guid.NewGuid():N}";
        var usedResetHash = $"used-{Guid.NewGuid():N}";
        var activeResetHash = $"active-{Guid.NewGuid():N}";

        await _auth.DbVoid(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            db.RefreshTokens.Add(new RefreshToken(
                user.Id, Guid.NewGuid(), expiredRefreshHash, now.AddTicks(-1), "127.0.0.1"));
            db.RefreshTokens.Add(new RefreshToken(
                user.Id, Guid.NewGuid(), activeRefreshHash, now.AddDays(1), "127.0.0.1"));

            var expiredReset = new PasswordResetToken(
                user.Id, expiredResetHash, now.AddTicks(-1), "127.0.0.1");
            var usedReset = new PasswordResetToken(
                user.Id, usedResetHash, now.AddDays(1), "127.0.0.1");
            usedReset.MarkUsed(now);
            db.PasswordResetTokens.AddRange(expiredReset, usedReset);
            db.PasswordResetTokens.Add(new PasswordResetToken(
                user.Id, activeResetHash, now.AddDays(1), "127.0.0.1"));
            await db.SaveChangesAsync();
        });

        await using var scope = _auth.Services.CreateAsyncScope();
        var cleanup = scope.ServiceProvider.GetRequiredService<ExpiredTokenCleanup>();
        var deleted = await cleanup.RunOnceAsync();

        Assert.True(deleted.RefreshTokens >= 1);
        Assert.True(deleted.PasswordResetTokens >= 2);
        Assert.False(await _auth.Db(db =>
            db.RefreshTokens.AnyAsync(token => token.TokenHash == expiredRefreshHash)));
        Assert.True(await _auth.Db(db =>
            db.RefreshTokens.AnyAsync(token => token.TokenHash == activeRefreshHash)));
        Assert.False(await _auth.Db(db =>
            db.PasswordResetTokens.AnyAsync(token =>
                token.TokenHash == expiredResetHash || token.TokenHash == usedResetHash)));
        Assert.True(await _auth.Db(db =>
            db.PasswordResetTokens.AnyAsync(token => token.TokenHash == activeResetHash)));
        Assert.True(await _auth.Db(db =>
            db.RefreshTokens.AnyAsync(token => token.UserId == session.User.Id)));
        Assert.True(await _auth.Db(db =>
            db.PasswordResetTokens.AnyAsync(token => token.UserId == session.User.Id)));
    }
}