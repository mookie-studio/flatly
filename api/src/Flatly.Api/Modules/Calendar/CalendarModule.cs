namespace Flatly.Api.Modules.Calendar;

/// <summary>Registration of the Calendar module: shared calendar, rules and notice board (MVP2).</summary>
public static class CalendarModule
{
    /// <summary>Route prefix of every endpoint in this module.</summary>
    public const string RoutePrefix = "/api/v1/calendar";

    /// <summary>Registers the services this module needs.</summary>
    public static IServiceCollection AddCalendarModule(this IServiceCollection services) => services;

    /// <summary>Maps the endpoints of this module under <see cref="RoutePrefix"/>.</summary>
    public static IEndpointRouteBuilder MapCalendarModule(this IEndpointRouteBuilder app) =>
        app.MapGroup(RoutePrefix).WithTags("Calendar");
}
