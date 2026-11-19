using Flatly.Api.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Flatly.Api.Modules.Platform.Endpoints;

/// <summary>Request of the sample endpoint that demonstrates field-keyed validation errors.</summary>
/// <param name="Name">Required, at most 50 characters.</param>
public sealed record ValidationSampleRequest(string? Name);

/// <summary>Checks <see cref="ValidationSampleRequest"/>. Errors are keyed by the JSON field name.</summary>
public sealed class ValidationSampleRequestValidator : IRequestValidator<ValidationSampleRequest>
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string[]> Validate(ValidationSampleRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (request.Name.Length > 50)
        {
            errors["name"] = ["Name must be 50 characters or fewer."];
        }

        return errors;
    }
}

/// <summary>
/// Sample endpoint for the validation filter and error contract. Remove it once the first real endpoint
/// of a module uses the same pattern.
/// </summary>
public static class ValidationSampleEndpoint
{
    /// <summary>Maps <c>POST validation-sample</c> under the given group.</summary>
    public static RouteGroupBuilder MapValidationSample(this RouteGroupBuilder group)
    {
        group.MapPost("/validation-sample", (ValidationSampleRequest request) => TypedResults.Ok(request))
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<ValidationSampleRequest>>()
            .WithName("ValidationSample")
            .WithSummary("Sample endpoint showing field-keyed validation errors (400 ProblemDetails).");
        return group;
    }
}
