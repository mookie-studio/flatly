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

/// <summary>Checks <see cref="UpdateChoreRequest"/>: only the fields that are sent are checked, with the same rules.</summary>
public sealed class UpdateChoreRequestValidator : IRequestValidator<UpdateChoreRequest>
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string[]> Validate(UpdateChoreRequest request) =>
        ChoreRules.Check(
            new ChoreFields(request.Name, request.RecurrenceUnit, request.RecurrenceEvery, request.Weight, request.ParticipantIds),
            requireAll: false);
}
