using Flatly.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Flatly.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in-process, on the given PostgreSQL, with a clock the test controls.
/// The Development environment applies the migrations when the host starts, as it does locally.
/// </summary>
public sealed class FlatlyApiFactory(string connectionString, DateTimeOffset now) : WebApplicationFactory<Program>
{
    /// <summary>The clock the API reads time from. Set or advance it in a test to move the application's time.</summary>
    public FakeTimeProvider Clock { get; } = new(now);

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting($"ConnectionStrings:{DatabaseRegistration.ConnectionName}", connectionString);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}
