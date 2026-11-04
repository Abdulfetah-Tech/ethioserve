using System.ComponentModel.DataAnnotations;
using Amazon;
using Amazon.SimpleEmailV2;
using EthioServe.Application.Abstractions;
using EthioServe.Application.Auth;
using EthioServe.Infrastructure.Identity;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EthioServe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Use 'dotnet user-secrets' locally or an environment variable.");

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<EthioServe.Application.Abstractions.IFileStorage>(sp => new EthioServe.Infrastructure.Storage.LocalFileStorage(sp.GetService<Microsoft.Extensions.Configuration.IConfiguration>()?["Storage:RootPath"]));
        // Fails fast at startup if the JWT settings are missing or invalid (for example, no signing key).
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var emailProvider = configuration[$"{EmailOptions.SectionName}:Provider"]?.Trim();
        var useSes = string.Equals(emailProvider, "Ses", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(emailProvider)
            && !string.Equals(emailProvider, "None", StringComparison.OrdinalIgnoreCase)
            && !useSes)
        {
            throw new InvalidOperationException("Email:Provider must be 'None' or 'Ses'.");
        }

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(options =>
                    string.Equals(options.Provider, "None", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(options.Provider, "Ses", StringComparison.OrdinalIgnoreCase),
                "Email:Provider must be 'None' or 'Ses'.")
            .Validate(options =>
                    !string.Equals(options.Provider, "Ses", StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(options.SenderAddress)
                        && new EmailAddressAttribute().IsValid(options.SenderAddress)),
                "Email:SenderAddress must be a valid email address when using SES.")
            .Validate(options =>
                    !string.Equals(options.Provider, "Ses", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(options.Region),
                "Email:Region is required when using SES.")
            .ValidateOnStart();

        if (useSes)
        {
            services.AddSingleton<IAmazonSimpleEmailServiceV2>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<EmailOptions>>().Value;
                var clientConfig = new AmazonSimpleEmailServiceV2Config
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region)
                };
                return new AmazonSimpleEmailServiceV2Client(clientConfig);
            });
            services.AddSingleton<IEmailService, SesEmailService>();
        }
        else
        {
            services.AddSingleton<IEmailService, DevelopmentEmailService>();
        }

        services.AddScoped<ExpiredTokenCleanup>();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = PasswordPolicy.MinLength;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IAuthService, AuthService>();
        

        return services;
    }
}