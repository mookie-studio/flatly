using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Data;

/// <summary>
/// The application's single PostgreSQL context. It holds the ASP.NET Core Identity tables for accounts and their
/// external logins. Other tables are added by the feature tasks that need them.
/// Column names are snake_case (see <see cref="DatabaseRegistration"/>), and every timestamp is a UTC <c>timestamptz</c>.
/// </summary>
public class FlatlyDbContext : IdentityUserContext<FlatlyUser, Guid>
{
    /// <summary>Creates the context with the options registered by the host.</summary>
    public FlatlyDbContext(DbContextOptions<FlatlyDbContext> options) : base(options)
    {
    }

    /// <summary>Creates the context from non-generic options, for derived contexts such as test models.</summary>
    protected FlatlyDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every date and time is a timestamptz. Npgsql only accepts UTC DateTime values for timestamptz,
        // so a local or unspecified value fails on write instead of being stored with the wrong offset.
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamptz");
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Short table names that match the rest of the schema, instead of Identity's AspNet* names.
        builder.Entity<FlatlyUser>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.DisplayName).HasMaxLength(ExternalAccountService.MaxDisplayNameLength).IsRequired();
            user.Property(u => u.AvatarUrl).HasMaxLength(2048);
            user.Property(u => u.PreferredLanguage).HasMaxLength(5).IsRequired();
            user.Property(u => u.TimeZoneId).HasMaxLength(64).IsRequired();
            user.Property(u => u.CreatedAt).IsRequired();
            user.ToTable(table => table.HasCheckConstraint(
                "ck_users_preferred_language",
                $"preferred_language in ('{UserLanguages.Spanish}', '{UserLanguages.English}')"));
        });
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
    }
}
