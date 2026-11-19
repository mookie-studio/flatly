using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flatly.Api.Data;

/// <summary>
/// Lets the <c>dotnet ef</c> tools create <see cref="FlatlyDbContext"/> without starting the host.
/// Creating or listing migrations only needs the provider, not a running server, so the connection string here is a placeholder.
/// </summary>
public sealed class FlatlyDbContextFactory : IDesignTimeDbContextFactory<FlatlyDbContext>
{
    /// <summary>Creates the context used for design-time work such as adding a migration.</summary>
    public FlatlyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FlatlyDbContext>()
            .UseNpgsql("Host=localhost;Database=flatly_design_time")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new FlatlyDbContext(options);
    }
}
