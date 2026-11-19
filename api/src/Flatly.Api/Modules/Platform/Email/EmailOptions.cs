namespace Flatly.Api.Modules.Platform.Email;

/// <summary>Which delivery provider sends the email. Values match the <c>Email:Provider</c> setting, case-insensitively.</summary>
public static class EmailProviders
{
    /// <summary>Plain SMTP. Local development uses Mailpit through it (see the AppHost).</summary>
    public const string Smtp = "Smtp";

    /// <summary>Resend, the transactional provider used in deployment.</summary>
    public const string Resend = "Resend";
}

/// <summary>Email settings, read from the <c>Email</c> section. Secrets come from configuration, never from the repository.</summary>
public sealed class EmailOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Email";

    /// <summary>The provider that sends the mail: <see cref="EmailProviders.Smtp"/> or <see cref="EmailProviders.Resend"/>.</summary>
    public string Provider { get; set; } = EmailProviders.Smtp;

    /// <summary>Sender address. Deployment must use an address on a domain verified with the provider.</summary>
    public string FromAddress { get; set; } = "no-reply@flatly.local";

    /// <summary>Sender display name.</summary>
    public string FromName { get; set; } = "Flatly";

    /// <summary>Settings for <see cref="EmailProviders.Smtp"/>.</summary>
    public SmtpOptions Smtp { get; set; } = new();

    /// <summary>Settings for <see cref="EmailProviders.Resend"/>.</summary>
    public ResendOptions Resend { get; set; } = new();
}

/// <summary>SMTP server settings. The defaults match Mailpit on the local machine.</summary>
public sealed class SmtpOptions
{
    /// <summary>Host name of the SMTP server.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Port of the SMTP server.</summary>
    public int Port { get; set; } = 1025;

    /// <summary>Whether the connection uses TLS.</summary>
    public bool EnableSsl { get; set; }

    /// <summary>User name, when the server requires authentication. Empty means anonymous.</summary>
    public string? UserName { get; set; }

    /// <summary>Password for <see cref="UserName"/>. Comes from a secret.</summary>
    public string? Password { get; set; }
}

/// <summary>Resend API settings.</summary>
public sealed class ResendOptions
{
    /// <summary>Base address of the Resend API.</summary>
    public string BaseUrl { get; set; } = "https://api.resend.com";

    /// <summary>API key. Comes from a secret, for example <c>Email__Resend__ApiKey</c> in App Service.</summary>
    public string? ApiKey { get; set; }
}
