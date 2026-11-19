using Flatly.Api.Data;
using Flatly.Api.Data.Tenancy;
using Flatly.Api.Modules.Flats.Domain;
using Xunit;

namespace Flatly.Api.Tests.Data.Tenancy;

/// <summary>Checks that every flat-scoped entity of <see cref="FlatlyDbContext"/> has the flat filter (ADR-002), without a database.</summary>
public sealed class FlatScopeModelTests
{
    [Fact]
    public void EveryFlatScopedEntityHasTheCurrentFlatFilter()
    {
        using var context = new FlatlyDbContextFactory().CreateDbContext([]);

        var scoped = context.Model.GetEntityTypes()
            .Where(entityType => typeof(IFlatScoped).IsAssignableFrom(entityType.ClrType))
            .ToList();

        Assert.Contains(scoped, entityType => entityType.ClrType == typeof(Membership));
        Assert.All(scoped, entityType => Assert.NotEmpty(entityType.GetDeclaredQueryFilters()));
    }

    [Fact]
    public void FlatTheTenantItselfIsNotFlatScoped()
    {
        using var context = new FlatlyDbContextFactory().CreateDbContext([]);

        var flat = context.Model.FindEntityType(typeof(Flat));

        Assert.NotNull(flat);
        Assert.False(typeof(IFlatScoped).IsAssignableFrom(flat.ClrType));
        Assert.Empty(flat.GetDeclaredQueryFilters());
    }
}
