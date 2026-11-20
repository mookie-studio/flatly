using Flatly.Api.Modules.Chores.Endpoints;
using Flatly.Api.Validation;

namespace Flatly.Api.Modules.Chores.Validation;

/// <summary>Checks <see cref="CreateChoreRequest"/>: every field is required and must follow <see cref="ChoreRules"/>.</summary>
public sealed class CreateChoreRequestValidator : IRequestValidator<CreateChoreRequest>
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string[]> Validate(CreateChoreRequest request) =>
        ChoreRules.Check(
            new ChoreFields(request.Name, request.RecurrenceUnit, request.RecurrenceEvery, request.Weight, request.ParticipantIds),
            requireAll: true);
}

/// <summary>
/// Checks <see cref="AddFromCatalogueRequest"/>: at least one known key, each at most once. Participants, when sent,
/// are non-empty and distinct; whether they are active members is checked by the endpoint, which has the database.
/// </summary>
public sealed class AddFromCatalogueRequestValidator : IRequestValidator<AddFromCatalogueRequest>
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string[]> Validate(AddFromCatalogueRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Keys is null || request.Keys.Count == 0)
        {
            errors["keys"] = ["Choose at least one predefined chore."];
        }
        else if (request.Keys.Any(key => Catalogue.PredefinedChores.Find(key) is null))
        {
            errors["keys"] = ["Every key must be a predefined chore of the catalogue."];
        }
        else if (request.Keys.Distinct().Count() != request.Keys.Count)
        {
            errors["keys"] = ["Each predefined chore can appear only once."];
        }

        if (request.ParticipantIds is not null)
        {
            if (request.ParticipantIds.Count == 0)
            {
                errors["participantIds"] = ["At least one participant is required."];
            }
            else if (request.ParticipantIds.Distinct().Count() != request.ParticipantIds.Count)
            {
                errors["participantIds"] = ["Each participant can appear only once."];
            }
        }

        return errors;
    }
}

/// <summary>Checks <see cref="UpdateChoreRequest"/>: only the fields that are sent are checked, with the same rules.</summary>
public sealed class UpdateChoreRequestValidator : IRequestValidator<UpdateChoreRequest>
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string[]> Validate(UpdateChoreRequest request) =>
        ChoreRules.Check(
            new ChoreFields(request.Name, request.RecurrenceUnit, request.RecurrenceEvery, request.Weight, request.ParticipantIds),
            requireAll: false);
}
