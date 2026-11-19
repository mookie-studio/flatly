namespace Flatly.Api.Modules.Platform.Identity;

/// <summary>The identity providers Flatly accepts. MVP1 uses Google and Microsoft only; Apple is added in MVP2 (#32).</summary>
public static class ExternalProviders
{
    /// <summary>Google accounts.</summary>
    public const string Google = "Google";

    /// <summary>Microsoft accounts, including personal Outlook and Hotmail accounts.</summary>
    public const string Microsoft = "Microsoft";

    /// <summary>True when the provider is one Flatly signs in with.</summary>
    public static bool IsSupported(string? provider) => provider is Google or Microsoft;
}

/// <summary>
/// What a provider tells us after a successful sign-in.
/// </summary>
/// <param name="Provider">Provider name, one of <see cref="ExternalProviders"/>.</param>
/// <param name="Subject">The provider's stable id for the account. This is what identifies the member.</param>
/// <param name="Email">Email the provider reports, if any. Used only to prefill, never to match accounts.</param>
/// <param name="DisplayName">Name the provider reports, if any.</param>
/// <param name="AvatarUrl">Picture URL the provider reports, if any.</param>
public sealed record ExternalIdentity(string Provider, string Subject, string? Email = null, string? DisplayName = null, string? AvatarUrl = null);
