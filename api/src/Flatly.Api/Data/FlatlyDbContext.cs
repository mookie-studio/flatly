using Flatly.Api.Data.Tenancy;
using Flatly.Api.Modules.Chores.Data;
using Flatly.Api.Modules.Chores.Domain;
using Flatly.Api.Modules.Flats.Data;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Notifications.Data;
using Flatly.Api.Modules.Notifications.Domain;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Data;

/// <summary>
/// The application's single PostgreSQL context. It holds the ASP.NET Core Identity tables for accounts and their
/// external logins, and the flats and their memberships, and the notifications. Other tables are added by the feature tasks that need them.
/// Column names are snake_case (see <see cref="DatabaseRegistration"/>), and every timestamp is a UTC <c>timestamptz</c>.
/// </summary>
public class FlatlyDbContext : IdentityUserContext<FlatlyUser, Guid>
{
    /// <summary>Flats, the tenants of the product (ADR-002).</summary>
    public DbSet<Flat> Flats => Set<Flat>();

    /// <summary>Who belongs to which flat, with the role and the join and leave dates.</summary>
    public DbSet<Membership> Memberships => Set<Membership>();

    /// <summary>Household chores of each flat, flat-scoped.</summary>
    public DbSet<Chore> Chores => Set<Chore>();

    /// <summary>Members who take part in the rotation of a chore, flat-scoped.</summary>
    public DbSet<ChoreParticipant> ChoreParticipants => Set<ChoreParticipant>();

    /// <summary>The turns of the chores of the flat in scope.</summary>
    public DbSet<Turn> Turns => Set<Turn>();

    /// <summary>In-app notifications, one row per recipient. Not flat-scoped: the inbox spans every flat of the member.</summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    private readonly CurrentFlat _currentFlat;

    /// <summary>Creates the context with the options and the current flat registered by the host.</summary>
    public FlatlyDbContext(DbContextOptions<FlatlyDbContext> options, CurrentFlat currentFlat) : base(options)
    {
        _currentFlat = currentFlat;
    }

    /// <summary>Creates the context from non-generic options, for derived contexts such as test models.</summary>
    protected FlatlyDbContext(DbContextOptions options, CurrentFlat currentFlat) : base(options)
    {
        _currentFlat = currentFlat;
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
        FlatsModelConfiguration.Configure(builder);

        // Tenant isolation (ADR-002): every flat-scoped entity is filtered to the flat of the current request.
        // Each IFlatScoped entity needs one line here; the unit tests fail when one is missing.
        FilterByCurrentFlat<Membership>(builder);

        ChoresModelConfiguration.Configure(builder);
        FilterByCurrentFlat<Chore>(builder);
        FilterByCurrentFlat<ChoreParticipant>(builder);
        FilterByCurrentFlat<Turn>(builder);

        NotificationsModelConfiguration.Configure(builder);
    }

    private void FilterByCurrentFlat<TEntity>(ModelBuilder builder) where TEntity : class, IFlatScoped =>
        builder.Entity<TEntity>().HasQueryFilter(entity => entity.FlatId == _currentFlat.Id);
}
