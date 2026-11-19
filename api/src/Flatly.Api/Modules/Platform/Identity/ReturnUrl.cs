namespace Flatly.Api.Modules.Platform.Identity;

/// <summary>
/// Decides where the browser goes after a sign-in. Only the web app's own origin is allowed, so the sign-in endpoint
/// cannot be used to send people to another site (open redirect).
/// </summary>
public static class ReturnUrl
{
    /// <summary>
    /// Returns <paramref name="returnUrl"/> when it is a path or an absolute URL on the web origin. Anything else,
    /// including a missing value, returns the web origin's home page.
    /// </summary>
    /// <param name="returnUrl">The address the web app asked to return to.</param>
    /// <param name="webOrigin">The web app's origin, for example <c>https://flatly.example</c>. Null when not configured.</param>
    public static string Resolve(string? returnUrl, string? webOrigin)
    {
        var origin = Uri.TryCreate(webOrigin, UriKind.Absolute, out var originUri)
            && originUri.Scheme is "https" or "http"
            ? originUri
            : null;
        var home = origin is null ? "/" : origin.GetLeftPart(UriPartial.Authority) + "/";

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return home;
        }

        // A path such as /flats. Reject "//host" and "/\host", which browsers read as another site.
        if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal) && !returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return origin is null ? returnUrl : origin.GetLeftPart(UriPartial.Authority) + returnUrl;
        }

        if (origin is not null
            && Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute)
            && absolute.Scheme is "https" or "http"
            && absolute.GetLeftPart(UriPartial.Authority) == origin.GetLeftPart(UriPartial.Authority))
        {
            return absolute.AbsoluteUri;
        }

        return home;
    }
}
