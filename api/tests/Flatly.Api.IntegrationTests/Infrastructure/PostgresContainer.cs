using Testcontainers.PostgreSql;
using Xunit;

namespace Flatly.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The PostgreSQL the integration tests run against: one container shared by the run, or the server named by
/// <see cref="DatabaseAvailability.ConnectionStringVariable"/>. The container is built and started on first use, so a
/// run that does not need it never touches Docker. Each test gets its own API host, and the migrations bring the schema up to date on start.
/// </summary>
public sealed class PostgresContainer : IAsyncLifetime
{
    /// <summary>Same major version as the production database.</summary>
    private const string Image = "postgres:17-alpine";

    private readonly Lazy<Task<PostgreSqlContainer>> _container;

    /// <summary>Creates the holder. Nothing starts until <see cref="ConnectionStringAsync"/> is called.</summary>
    public PostgresContainer()
    {
        _container = new Lazy<Task<PostgreSqlContainer>>(StartAsync);
    }

    /// <summary>Returns the connection string of the configured server, or starts the container and returns its own.</summary>
    public async Task<string> ConnectionStringAsync()
    {
        var configured = Environment.GetEnvironmentVariable(DatabaseAvailability.ConnectionStringVariable);
        return string.IsNullOrWhiteSpace(configured)
            ? (await _container.Value).GetConnectionString()
            : configured;
    }

    /// <inheritdoc />
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_container.IsValueCreated && Environment.GetEnvironmentVariable(DatabaseAvailability.ConnectionStringVariable) is null)
        {
            await (await _container.Value).DisposeAsync();
        }
    }

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        var container = new PostgreSqlBuilder(Image).Build();
        await container.StartAsync();
        return container;
    }
}

/// <summary>Collection that shares one <see cref="PostgresContainer"/> between the integration test classes.</summary>
[CollectionDefinition(Name)]
public sealed class SharedPostgres : ICollectionFixture<PostgresContainer>
{
    /// <summary>Name to put in <c>[Collection]</c>.</summary>
    public const string Name = "PostgreSQL";
}
