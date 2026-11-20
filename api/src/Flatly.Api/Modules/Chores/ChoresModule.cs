using Flatly.Api.Modules.Chores.Endpoints;
using Flatly.Api.Modules.Chores.Turns;
using Flatly.Api.Modules.Chores.Validation;
using Flatly.Api.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flatly.Api.Modules.Chores;

/// <summary>Registration of the Chores module: chores and their participants, flat-scoped (ADR-002).</summary>
public static class ChoresModule
{
    /// <summary>Route prefix of every endpoint in this module. Endpoints sit under <c>/{flatId}</c> (see <c>FlatRoute</c>).</summary>
    public const string RoutePrefix = "/api/v1/chores";

    /// <summary>Registers the services this module needs.</summary>
    public static IServiceCollection AddChoresModule(this IServiceCollection services)
    {
        services.AddSingleton<IRequestValidator<CreateChoreRequest>, CreateChoreRequestValidator>();
        services.AddSingleton<IRequestValidator<UpdateChoreRequest>, UpdateChoreRequestValidator>();
        services.TryAddScoped<TurnGenerator>();
        services.TryAddTransient<TurnHorizonJobs>();
        services.AddHostedService<TurnHorizonSchedule>();
        return services;
    }

    /// <summary>Maps the endpoints of this module under <see cref="RoutePrefix"/>.</summary>
    public static IEndpointRouteBuilder MapChoresModule(this IEndpointRouteBuilder app)
    {
        app.MapGroup(RoutePrefix)
            .WithTags("Chores")
            .MapListChores()
            .MapCreateChore()
            .MapUpdateChore()
            .MapTurns();
        return app;
    }
}
