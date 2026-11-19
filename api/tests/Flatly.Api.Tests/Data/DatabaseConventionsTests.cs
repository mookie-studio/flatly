using Flatly.Api.Data;
using Flatly.Api.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Flatly.Api.Tests.Data;

/// <summary>Checks the database conventions of <see cref="FlatlyDbContext"/> without a running PostgreSQL server.</summary>
public sealed class DatabaseConventionsTests
{
    [Fact]
    public void TablesAndColumnsAreSnakeCaseAndTimestampsAreTimestamptz()
    {
        var options = new DbContextOptionsBuilder()
            .UseNpgsql("Host=localhost;Database=flatly_tests")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var context = new ProbeContext(options);
        var entity = context.Model.FindEntityType(typeof(ProbeEntity))!;

        Assert.Equal("probes", entity.GetTableName());

        var createdAt = entity.FindProperty(nameof(ProbeEntity.CreatedAt))!;
        Assert.Equal("created_at", createdAt.GetColumnName());
        Assert.Equal("timestamptz", createdAt.GetColumnType());

        var expiresAt = entity.FindProperty(nameof(ProbeEntity.ExpiresAt))!;
        Assert.Equal("expires_at", expiresAt.GetColumnName());
        Assert.Equal("timestamptz", expiresAt.GetColumnType());
    }

    [Fact]
    public void ModelHasNoChangesWithoutAMigration()
    {
        using var context = new FlatlyDbContextFactory().CreateDbContext([]);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    /// <summary>Test-only context with one entity, so the conventions can be checked before any real table exists.</summary>
    private sealed class ProbeContext(DbContextOptions options) : FlatlyDbContext(options, new CurrentFlat())
    {
        public DbSet<ProbeEntity> Probes => Set<ProbeEntity>();
    }

    /// <summary>Test-only entity with one timestamp of each supported .NET type.</summary>
    public sealed class ProbeEntity
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }
    }
}
