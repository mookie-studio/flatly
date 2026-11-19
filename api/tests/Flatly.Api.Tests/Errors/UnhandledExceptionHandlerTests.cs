using System.Text;
using System.Text.Json;
using Flatly.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Flatly.Api.Tests.Errors;

public class UnhandledExceptionHandlerTests
{
    [Fact]
    public async Task UnhandledExceptionBecomes500ProblemDetailsWithTraceIdAndNoInternals()
    {
        var handler = new UnhandledExceptionHandler(NullLogger<UnhandledExceptionHandler>.Instance);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-123" };
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("connection string Password=hunter2"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        var body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.DoesNotContain("hunter2", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("StackTrace", body);

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("trace-123", root.GetProperty("traceId").GetString());
    }
}
