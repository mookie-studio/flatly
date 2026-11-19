namespace Flatly.Api.Modules.Notifications.Domain;

/// <summary>The area of the product a notification comes from. Members can choose per category how they are told (see #13).</summary>
public enum NotificationCategory
{
    /// <summary>Flat membership and administration.</summary>
    Flats,

    /// <summary>Chores and their rotation.</summary>
    Chores,

    /// <summary>Incidents and messages with the landlord.</summary>
    Maintenance,
}
