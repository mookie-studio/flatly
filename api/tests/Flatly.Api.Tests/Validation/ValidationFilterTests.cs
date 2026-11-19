using Flatly.Api.Modules.Platform.Endpoints;
using Flatly.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Flatly.Api.Tests.Validation;

public class ValidationFilterTests
{
    [Fact]
    public async Task InvalidRequestReturns400WithErrorsKeyedByFieldAndSkipsTheHandler()
    {
        var filter = new ValidationFilter<ValidationSampleRequest>([new ValidationSampleRequestValidator()]);
        var context = new FakeInvocationContext(new ValidationSampleRequest(" "));
        var nextCalled = false;

        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(null);
        });

        Assert.False(nextCalled);
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var validation = Assert.IsType<HttpValidationProblemDetails>(problem.ProblemDetails);
        Assert.Equal(["Name is required."], validation.Errors["name"]);
    }

    [Fact]
    public async Task ValidRequestCallsTheHandler()
    {
        var filter = new ValidationFilter<ValidationSampleRequest>([new ValidationSampleRequestValidator()]);
        var context = new FakeInvocationContext(new ValidationSampleRequest("Ana"));
        var nextCalled = false;

        await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(null);
        });

        Assert.True(nextCalled);
    }

    [Fact]
    public void ErrorsOfSeveralValidatorsForTheSameFieldAreMerged()
    {
        var filter = new ValidationFilter<ValidationSampleRequest>(
        [
            new StubValidator("name", "first"),
            new StubValidator("name", "second"),
        ]);

        var errors = filter.Validate(new ValidationSampleRequest("Ana"));

        Assert.Equal(["first", "second"], errors["name"]);
    }

    private sealed record StubValidator(string Field, string Message) : IRequestValidator<ValidationSampleRequest>
    {
        public IReadOnlyDictionary<string, string[]> Validate(ValidationSampleRequest request) =>
            new Dictionary<string, string[]> { [Field] = [Message] };
    }

    private sealed class FakeInvocationContext(object argument) : EndpointFilterInvocationContext
    {
        private readonly List<object?> _arguments = [argument];

        public override HttpContext HttpContext { get; } = new DefaultHttpContext();

        public override IList<object?> Arguments => _arguments;

        public override T GetArgument<T>(int index) => (T)_arguments[index]!;
    }
}
