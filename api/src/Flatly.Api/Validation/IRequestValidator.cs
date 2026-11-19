namespace Flatly.Api.Validation;

/// <summary>Validates one request type. Several validators may exist for the same type; their errors are merged.</summary>
/// <typeparam name="TRequest">The request type this validator checks.</typeparam>
public interface IRequestValidator<in TRequest>
{
    /// <summary>Returns the errors of <paramref name="request"/> keyed by field name (camelCase). Empty when valid.</summary>
    IReadOnlyDictionary<string, string[]> Validate(TRequest request);
}
