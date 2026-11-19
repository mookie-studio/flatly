using Flatly.Api.Modules.Notifications.Endpoints;
using Flatly.Api.Modules.Notifications.Services;

namespace Flatly.Api.Modules.Notifications;

/// <summary>Registration of the Notifications module: the in-app inbox and the internal API that modules use to notify.</summary>
public static class NotificationsModule
{
    /// <summary>Route prefix of every endpoint in this module.</summary>
    public const string RoutePrefix = "/api/v1/notifications";

    /// <summary>Registers the services this module needs.</summary>
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }

    /// <summary>Maps the endpoints of this module under <see cref="RoutePrefix"/>.</summary>
    public static IEndpointRouteBuilder MapNotificationsModule(this IEndpointRouteBuilder app)
    {
        app.MapGroup(RoutePrefix)
            .WithTags("Notifications")
            .MapListNotifications()
            .MapGetUnreadCount()
            .MapMarkNotificationsRead();
        return app;
    }
}
