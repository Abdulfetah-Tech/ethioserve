using EthioServe.Application.Abstractions;
using EthioServe.Infrastructure;
using EthioServe.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EthioServe.Tests.Unit;

public sealed class EmailProviderRegistrationTests
{
    [Fact]
    public void Uses_the_development_service_when_email_delivery_is_not_configured()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration());

        var registration = Assert.Single(
            services, descriptor => descriptor.ServiceType == typeof(IEmailService));
        Assert.Equal(typeof(DevelopmentEmailService), registration.ImplementationType);
    }

    [Fact]
    public void Selects_SES_when_configured_with_a_valid_sender()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration(
            ("Email:Provider", "Ses"),
            ("Email:SenderAddress", "no-reply@example.com"),
            ("Email:Region", "eu-west-2")));

        var registration = Assert.Single(
            services, descriptor => descriptor.ServiceType == typeof(IEmailService));
        Assert.Equal(typeof(SesEmailService), registration.ImplementationType);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<EmailOptions>>().Value;
        Assert.Equal("eu-west-2", options.Region);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Rejects_SES_configuration_without_a_valid_sender_address(string senderAddress)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Configuration(
            ("Email:Provider", "Ses"),
            ("Email:SenderAddress", senderAddress)));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<EmailOptions>>().Value);
    }

    [Fact]
    public void Rejects_unknown_email_providers_instead_of_falling_back()
    {
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(Configuration(("Email:Provider", "Unknown"))));

        Assert.Contains("Email:Provider", error.Message);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=ethioserve;Username=test;Password=test"
        };

        foreach (var (key, value) in values)
        {
            settings[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
