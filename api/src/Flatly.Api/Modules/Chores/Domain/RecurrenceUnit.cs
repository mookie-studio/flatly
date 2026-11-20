namespace Flatly.Api.Modules.Chores.Domain;

/// <summary>
/// The unit of a chore's recurrence. A chore recurs every <c>RecurrenceEvery</c> units: every 2 weeks is
/// <see cref="Week"/> with 2. Stored as text, so the database reads the same names as the API.
/// </summary>
public enum RecurrenceUnit
{
    /// <summary>Days.</summary>
    Day,

    /// <summary>Weeks.</summary>
    Week,

    /// <summary>Months.</summary>
    Month,
}
