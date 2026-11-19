using Flatly.Api.Modules.Flats.Data;
using Flatly.Api.Modules.Flats.Endpoints;
using Flatly.Api.Validation;

namespace Flatly.Api.Modules.Flats.Validation;

/// <summary>Checks <see cref="CreateFlatRequest"/>: the name is required and at most 80 characters after trimming.</summary>
public sealed class CreateFlatRequestValidator : IRequestValidator<CreateFlatRequest>
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string[]> Validate(CreateFlatRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (name.Length > FlatsModelConfiguration.MaxNameLength)
        {
            errors["name"] = [$"Name must be {FlatsModelConfiguration.MaxNameLength} characters or fewer."];
        }

        return errors;
    }
}
