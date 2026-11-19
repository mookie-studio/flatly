using Microsoft.AspNetCore.Identity;

namespace Flatly.Api.Modules.Platform.Identity;

/// <summary>
/// A Flatly account. It is created on the first external sign-in (see <see cref="ExternalAccountService"/>) and has no
/// local password. The provider's email, if any, is kept for display and contact only; it never identifies the account.
/// </summary>
public sealed class FlatlyUser : IdentityUser<Guid>
{
    /// <summary>Name shown to the other members of a flat. Prefilled from the provider on the first sign-in.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Absolute https URL of the avatar, or null when there is none.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Interface language: one of <see cref="UserLanguages"/>.</summary>
    public string PreferredLanguage { get; set; } = UserLanguages.Spanish;

    /// <summary>IANA time zone identifier, for example <c>Europe/Madrid</c>.</summary>
    public string TimeZoneId { get; set; } = UserLanguages.DefaultTimeZoneId;

    /// <summary>When the account was created, in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the member deleted the account, in UTC, or null while it is active. A deleted account keeps its row, so the
    /// memberships that point at it stay valid as history; its personal data is removed (see <c>AccountEndpoints</c>).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>The interface languages Flatly ships with.</summary>
public static class UserLanguages
{
    /// <summary>Spanish, the default language.</summary>
    public const string Spanish = "es";

    /// <summary>English.</summary>
    public const string English = "en";

    /// <summary>Time zone given to a new account until the member changes it.</summary>
    public const string DefaultTimeZoneId = "Europe/Madrid";

    /// <summary>True when <paramref name="language"/> is one of the supported languages.</summary>
    public static bool IsSupported(string? language) => language is Spanish or English;
}
