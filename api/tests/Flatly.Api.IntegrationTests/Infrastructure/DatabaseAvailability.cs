using Docker.DotNet;

namespace Flatly.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Tells whether the integration tests can reach a PostgreSQL: either an existing server named by
/// <see cref="ConnectionStringVariable"/>, or a Docker daemon that Testcontainers can start one on.
/// Without either, the tests skip with <see cref="SkipReason"/> instead of failing.
/// </summary>
public static class DatabaseAvailability
{
    /// <summary>
    /// Optional environment variable with the connection string of a PostgreSQL to use instead of a container,
    /// for example a local server. The database must be empty or hold only the Flatly schema.
    /// </summary>
    public const string ConnectionStringVariable = "FLATLY_TEST_CONNECTION_STRING";

    /// <summary>Reason shown on every skipped integration test.</summary>
    public const string SkipReason =
        $"No PostgreSQL available: start Docker, or set {ConnectionStringVariable} to an existing server.";

    private static readonly Lazy<Task<bool>> DockerRunning = new(CheckDockerAsync);

    /// <summary>True when a PostgreSQL connection string is configured or the Docker daemon answers a ping.</summary>
    public static async Task<bool> IsAvailableAsync() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable))
        || await DockerRunning.Value;

    private static async Task<bool> CheckDockerAsync()
    {
        try
        {
            using var client = new DockerClientBuilder().Build();
            await client.System.PingAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
