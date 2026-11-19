using Microsoft.AspNetCore.Http;

namespace Flatly.Api.Validation;

/// <summary>
/// Endpoint filter that runs every <see cref="IRequestValidator{TRequest}"/> registered for
/// <typeparamref name="TRequest"/> before the handler. Invalid input gets a 400 ProblemDetails whose
/// <c>errors</c> dictionary is keyed by field name.
/// Attach with <c>.AddEndpointFilter&lt;ValidationFilter&lt;TRequest&gt;&gt;()</c>.
/// </summary>
public sealed class ValidationFilter<TRequest>(IEnumerable<IRequestValidator<TRequest>> validators) : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is not null)
        {
            var errors = Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }
        }

        return await next(context);
    }

    /// <summary>Runs every validator and merges their errors by field name.</summary>
    public Dictionary<string, string[]> Validate(TRequest request)
    {
        var merged = new Dictionary<string, List<string>>();
        foreach (var validator in validators)
        {
            foreach (var (field, messages) in validator.Validate(request))
            {
                if (!merged.TryGetValue(field, out var list))
                {
                    merged[field] = list = [];
                }

                list.AddRange(messages);
            }
        }

        return merged.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }
}
