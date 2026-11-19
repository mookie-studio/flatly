using Flatly.Api.Modules.Flats.Endpoints;
using Flatly.Api.Validation;

namespace Flatly.Api.Modules.Flats.Validation;

/// <summary>Checks <see cref="ChangeMemberRoleRequest"/>: the role must be <c>Admin</c> or <c>Member</c>.</summary>
public sealed class ChangeMemberRoleRequestValidator : IRequestValidator<ChangeMemberRoleRequest>
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string[]> Validate(ChangeMemberRoleRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Role is not ("Admin" or "Member"))
        {
            errors["role"] = ["Role must be Admin or Member."];
        }

        return errors;
    }
}
