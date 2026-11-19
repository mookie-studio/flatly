using Flatly.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Platform.Identity;

/// <summary>
/// Finds the Flatly account behind an external sign-in, or creates it the first time.
/// Accounts are matched by provider and subject only (ADR-003). The same email arriving from another provider
/// creates a separate account; linking accounts is not part of MVP1.
/// </summary>
public sealed class ExternalAccountService(UserManager<FlatlyUser> users, FlatlyDbContext database, TimeProvider clock)
{
    /// <summary>Longest display name kept. Longer provider names are cut to this length.</summary>
    public const int MaxDisplayNameLength = 80;

    /// <summary>Returns the account linked to this provider subject, creating it when none exists yet.</summary>
    /// <exception cref="ArgumentException">The provider is not supported or the subject is empty.</exception>
    /// <exception cref="InvalidOperationException">The provider's data could not be stored as an account.</exception>
    public async Task<FlatlyUser> FindOrCreateAsync(ExternalIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!ExternalProviders.IsSupported(identity.Provider))
        {
            throw new ArgumentException($"The provider '{identity.Provider}' is not supported.", nameof(identity));
        }
        if (string.IsNullOrWhiteSpace(identity.Subject))
        {
            throw new ArgumentException("The provider subject is required.", nameof(identity));
        }

        var existing = await users.FindByLoginAsync(identity.Provider, identity.Subject);
        if (existing is not null)
        {
            return existing;
        }

        try
        {
            return await CreateAsync(identity, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request created the same account between the lookup and the insert. The unique login index
            // rejected this one, and the transaction rolled back, so the account now exists and can be read.
            database.ChangeTracker.Clear();
            return await users.FindByLoginAsync(identity.Provider, identity.Subject)
                ?? throw new InvalidOperationException("The account could not be created or found.");
        }
    }

    private async Task<FlatlyUser> CreateAsync(ExternalIdentity identity, CancellationToken cancellationToken)
    {
        // The user and its login are written in one transaction, so a failed login link never leaves an account behind.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var email = string.IsNullOrWhiteSpace(identity.Email) ? null : identity.Email.Trim();
        var user = new FlatlyUser
        {
            Id = Guid.NewGuid(),
            // The username is not shown anywhere. A random value keeps it unique and free of personal data.
            UserName = Guid.NewGuid().ToString("N"),
            Email = email,
            DisplayName = DisplayNameFrom(identity),
            AvatarUrl = AvatarUrlFrom(identity.AvatarUrl),
            CreatedAt = clock.GetUtcNow(),
        };

        // No password: CreateAsync(user) without one, and no email confirmation.
        ThrowIfFailed(await users.CreateAsync(user));
        // The login is added directly, not through UserManager.AddLoginAsync: that call checks for an existing login first,
        // and a concurrent request can commit one right after the check. The primary key of the login table then rejects
        // this insert with DbUpdateException, which FindOrCreateAsync handles.
        database.UserLogins.Add(new IdentityUserLogin<Guid>
        {
            LoginProvider = identity.Provider,
            ProviderKey = identity.Subject,
            ProviderDisplayName = identity.Provider,
            UserId = user.Id,
        });
        await database.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return user;
    }

    private static string DisplayNameFrom(ExternalIdentity identity)
    {
        var name = identity.DisplayName?.Trim();
        if (string.IsNullOrEmpty(name) && identity.Email is { } email)
        {
            // Before the @ of the address, for providers that do not report a name.
            name = email.Trim().Split('@')[0];
        }

        return string.IsNullOrEmpty(name)
            ? "Member"
            : name[..Math.Min(name.Length, MaxDisplayNameLength)];
    }

    /// <summary>Keeps only absolute https URLs, so a provider value never becomes a link to anything else.</summary>
    private static string? AvatarUrlFrom(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.AbsoluteUri.Length <= 2048
            ? uri.AbsoluteUri
            : null;

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
