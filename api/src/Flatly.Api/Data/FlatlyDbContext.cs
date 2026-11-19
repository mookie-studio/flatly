using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Data;

/// <summary>
/// The application's single PostgreSQL context. Tables are added by the feature tasks that need them.
/// Column names are snake_case (see <see cref="DatabaseRegistration"/>), and every timestamp is a UTC <c>timestamptz</c>.
/// </summary>
public class FlatlyDbContext : DbContext
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
}
