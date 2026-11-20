using Flatly.Api.Modules.Chores.Domain;

namespace Flatly.Api.Modules.Chores.Validation;

/// <summary>
/// The fields of a chore as a request sends them. A null field is absent: on create every field is required, on
/// update only the fields that are sent are checked.
/// </summary>
public sealed record ChoreFields(
    string? Name,
    string? RecurrenceUnit,
    int? RecurrenceEvery,
    int? Weight,
    IReadOnlyList<Guid>? ParticipantIds);

/// <summary>The rules every chore must follow, shared by the create and update validators.</summary>
public static class ChoreRules
{
    /// <summary>Longest chore name kept, after trimming.</summary>
    public const int MaxNameLength = 80;

    /// <summary>Smallest recurrence interval.</summary>
    public const int MinRecurrenceEvery = 1;

    /// <summary>Largest recurrence interval, in the chore's unit.</summary>
    public const int MaxRecurrenceEvery = 365;

    /// <summary>Lightest weight.</summary>
    public const int MinWeight = 1;

    /// <summary>Heaviest weight.</summary>
    public const int MaxWeight = 5;

    private static readonly string[] RecurrenceUnitNames = Enum.GetNames<RecurrenceUnit>();

    /// <summary>Checks the fields and returns the errors keyed by camelCase field name. Empty when valid.</summary>
    /// <param name="fields">The fields to check.</param>
    /// <param name="requireAll">True on create, where every field is required; false on update.</param>
    public static IReadOnlyDictionary<string, string[]> Check(ChoreFields fields, bool requireAll)
    {
        var errors = new Dictionary<string, string[]>();

        if (fields.Name is null)
        {
            if (requireAll)
            {
                errors["name"] = ["Name is required."];
            }
        }
        else
        {
            var name = fields.Name.Trim();
            if (name.Length == 0)
            {
                errors["name"] = ["Name is required."];
            }
            else if (name.Length > MaxNameLength)
            {
                errors["name"] = [$"Name must be {MaxNameLength} characters or fewer."];
            }
        }

        if (fields.RecurrenceUnit is null)
        {
            if (requireAll)
            {
                errors["recurrenceUnit"] = ["Recurrence unit is required."];
            }
        }
        else if (Array.IndexOf(RecurrenceUnitNames, fields.RecurrenceUnit) < 0)
        {
            errors["recurrenceUnit"] = [$"Recurrence unit must be {string.Join(", ", RecurrenceUnitNames)}."];
        }

        if (fields.RecurrenceEvery is null)
        {
            if (requireAll)
            {
                errors["recurrenceEvery"] = ["Recurrence interval is required."];
            }
        }
        else if (fields.RecurrenceEvery < MinRecurrenceEvery || fields.RecurrenceEvery > MaxRecurrenceEvery)
        {
            errors["recurrenceEvery"] = [$"Recurrence interval must be between {MinRecurrenceEvery} and {MaxRecurrenceEvery}."];
        }

        if (fields.Weight is null)
        {
            if (requireAll)
            {
                errors["weight"] = ["Weight is required."];
            }
        }
        else if (fields.Weight < MinWeight || fields.Weight > MaxWeight)
        {
            errors["weight"] = [$"Weight must be between {MinWeight} and {MaxWeight}."];
        }

        if (fields.ParticipantIds is null)
        {
            if (requireAll)
            {
                errors["participantIds"] = ["At least one participant is required."];
            }
        }
        else if (fields.ParticipantIds.Count == 0)
        {
            errors["participantIds"] = ["At least one participant is required."];
        }
        else if (fields.ParticipantIds.Distinct().Count() != fields.ParticipantIds.Count)
        {
            errors["participantIds"] = ["Each participant can appear only once."];
        }

        return errors;
    }
}
