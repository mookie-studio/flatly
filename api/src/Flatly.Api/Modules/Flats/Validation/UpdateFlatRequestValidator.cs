using Flatly.Api.Modules.Flats.Endpoints;
using Flatly.Api.Validation;

namespace Flatly.Api.Modules.Flats.Validation;

/// <summary>Checks <see cref="UpdateFlatRequest"/> with the same rules as a new flat's name.</summary>
public sealed class UpdateFlatRequestValidator : IRequestValidator<UpdateFlatRequest>
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string[]> Validate(UpdateFlatRequest request) =>
        new CreateFlatRequestValidator().Validate(new CreateFlatRequest(request.Name));
}
