using Flatly.Api.Modules.Flats.Endpoints;
using Flatly.Api.Modules.Flats.Invites;
using Flatly.Api.Validation;

namespace Flatly.Api.Modules.Flats.Validation;

/// <summary>Checks <see cref="JoinFlatRequest"/>: the code is required and has the length of an invite code.</summary>
public sealed class JoinFlatRequestValidator : IRequestValidator<JoinFlatRequest>
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string[]> Validate(JoinFlatRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var code = request.Code?.Trim();
        if (string.IsNullOrEmpty(code))
        {
            errors["code"] = ["Code is required."];
        }
        else if (InviteCodes.Normalize(code).Length != InviteCodes.Length)
        {
            errors["code"] = [$"Code must be {InviteCodes.Length} characters."];
        }

        return errors;
    }
}
