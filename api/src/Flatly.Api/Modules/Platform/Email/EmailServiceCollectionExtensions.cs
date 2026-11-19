using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Flatly.Api.Modules.Platform.Email;

/// <summary>Registers <see cref="IEmailSender"/> and its provider.</summary>
public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Binds the <c>Email</c> section, checks it at startup and registers the provider it names.
    /// Unknown provider names and a missing Resend key stop the host instead of failing on the first email.
    /// </summary>
    public static IServiceCollection AddEmailSender(this IServiceCollection services)
    {
        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .Validate(
                options => EmailProviders.Smtp.Equals(options.Provider, StringComparison.OrdinalIgnoreCase)
                    || EmailProviders.Resend.Equals(options.Provider, StringComparison.OrdinalIgnoreCase),
                "Email:Provider must be 'Smtp' or 'Resend'.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.FromAddress), "Email:FromAddress is required.")
            .Validate(
                options => !EmailProviders.Resend.Equals(options.Provider, StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(options.Resend.ApiKey),
                "Email:Resend:ApiKey is required when Email:Provider is 'Resend'.")
            .ValidateOnStart();

        services.TryAddTransient<SmtpEmailSender>();
        services.AddHttpClient<ResendEmailSender>();

        services.TryAddSingleton<IEmailSender>(sp =>
        {
            var provider = sp.GetRequiredService<IOptions<EmailOptions>>().Value.Provider;
            return EmailProviders.Resend.Equals(provider, StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<ResendEmailSender>()
                : sp.GetRequiredService<SmtpEmailSender>();
        });
        return services;
    }
}
