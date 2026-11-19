using Flatly.Api.Modules.Platform.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Flatly.Api.Tests.Platform.Email;

public class EmailSenderRegistrationTests
{
    private static ServiceProvider Build(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddEmailSender();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void DefaultProviderIsSmtpToMailpit()
    {
        using var provider = Build();

        Assert.IsType<SmtpEmailSender>(provider.GetRequiredService<IEmailSender>());
        var options = provider.GetRequiredService<IOptions<EmailOptions>>().Value;
        Assert.Equal("localhost", options.Smtp.Host);
        Assert.Equal(1025, options.Smtp.Port);
    }

    [Fact]
    public void ResendProviderIsSelectedFromConfiguration()
    {
        using var provider = Build(
            ("Email:Provider", "Resend"),
            ("Email:FromAddress", "avisos@flatly.example"),
            ("Email:Resend:ApiKey", "test-key"));

        Assert.IsType<ResendEmailSender>(provider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void ResendWithoutApiKeyIsRejected()
    {
        using var provider = Build(
            ("Email:Provider", "Resend"),
            ("Email:FromAddress", "avisos@flatly.example"));

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EmailOptions>>().Value);
    }

    [Fact]
    public void UnknownProviderIsRejected()
    {
        using var provider = Build(("Email:Provider", "Carrier pigeon"));

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EmailOptions>>().Value);
    }
}
