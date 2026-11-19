using System.Security.Claims;
using Flatly.Api.Modules.Platform.Identity;
using Flatly.Api.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Flatly.Api.Modules.Platform.Endpoints;

/// <summary>The profile of the signed-in member.</summary>
/// <param name="DisplayName">Name shown to the other members of the flat.</param>
/// <param name="AvatarUrl">Absolute https URL of the avatar, or null when there is none.</param>
/// <param name="PreferredLanguage">Interface language: <c>es</c> or <c>en</c>.</param>
/// <param name="TimeZoneId">IANA time zone identifier, for example <c>Europe/Madrid</c>.</param>
public sealed record ProfileResponse(string DisplayName, string? AvatarUrl, string PreferredLanguage, string TimeZoneId);

/// <summary>
/// The full profile sent to <c>PUT profile</c>. Every field replaces the stored value, so an absent or empty
/// <c>avatarUrl</c> removes the avatar.
/// </summary>
public sealed record UpdateProfileRequest(string? DisplayName, string? AvatarUrl, string? PreferredLanguage, string? TimeZoneId);

/// <summary>Checks <see cref="UpdateProfileRequest"/>. Errors are keyed by the JSON field name.</summary>
public sealed class UpdateProfileRequestValidator : IRequestValidator<UpdateProfileRequest>
{
    /// <summary>Longest time zone identifier accepted.</summary>
    private const int MaxTimeZoneIdLength = 64;

    /// <summary>Longest avatar URL accepted.</summary>
    private const int MaxAvatarUrlLength = 2048;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string[]> Validate(UpdateProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            errors["displayName"] = ["Name is required."];
        }
        else if (displayName.Length > ExternalAccountService.MaxDisplayNameLength)
        {
            errors["displayName"] = [$"Name must be {ExternalAccountService.MaxDisplayNameLength} characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(request.AvatarUrl) && !IsHttpsUrl(request.AvatarUrl.Trim()))
        {
            errors["avatarUrl"] = ["Avatar must be an absolute https URL."];
        }

        if (!UserLanguages.IsSupported(request.PreferredLanguage))
        {
            errors["preferredLanguage"] = [$"Language must be '{UserLanguages.Spanish}' or '{UserLanguages.English}'."];
        }

        var timeZoneId = request.TimeZoneId?.Trim();
        if (string.IsNullOrEmpty(timeZoneId) || timeZoneId.Length > MaxTimeZoneIdLength
            || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
        {
            errors["timeZoneId"] = ["Time zone must be a valid IANA identifier, for example Europe/Madrid."];
        }

        return errors;
    }

    private static bool IsHttpsUrl(string url) =>
        url.Length <= MaxAvatarUrlLength
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
}

/// <summary>Endpoints that read and update the signed-in member's profile.</summary>
public static class ProfileEndpoints
{
    /// <summary>Maps <c>GET profile</c> and <c>PUT profile</c> under the given group.</summary>
    public static RouteGroupBuilder MapProfile(this RouteGroupBuilder group)
    {
        group.MapGet("/profile", GetProfile)
            .WithName("GetProfile")
            .WithSummary("Returns the signed-in member's profile.");

        group.MapPut("/profile", UpdateProfile)
            .AddEndpointFilter<ValidationFilter<UpdateProfileRequest>>()
            .WithName("UpdateProfile")
            .WithSummary("Replaces the signed-in member's profile: name, avatar, language and time zone.");
        return group;
    }

    private static async Task<Results<Ok<ProfileResponse>, UnauthorizedHttpResult>> GetProfile(
        ClaimsPrincipal principal,
        CurrentAccountResolver accounts,
        CancellationToken cancellationToken)
    {
        var member = await accounts.FindAsync(principal, cancellationToken);
        return member is null ? TypedResults.Unauthorized() : TypedResults.Ok(ToResponse(member));
    }

    private static async Task<Results<Ok<ProfileResponse>, UnauthorizedHttpResult>> UpdateProfile(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        CurrentAccountResolver accounts,
        UserManager<FlatlyUser> users,
        CancellationToken cancellationToken)
    {
        var member = await accounts.FindAsync(principal, cancellationToken);
        if (member is null)
        {
            return TypedResults.Unauthorized();
        }

        // The filter has already checked the request, so the required fields are present.
        member.DisplayName = request.DisplayName!.Trim();
        member.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        member.PreferredLanguage = request.PreferredLanguage!;
        member.TimeZoneId = request.TimeZoneId!.Trim();

        ThrowIfFailed(await users.UpdateAsync(member));
        return TypedResults.Ok(ToResponse(member));
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    private static ProfileResponse ToResponse(FlatlyUser member) =>
        new(member.DisplayName, member.AvatarUrl, member.PreferredLanguage, member.TimeZoneId);
}
