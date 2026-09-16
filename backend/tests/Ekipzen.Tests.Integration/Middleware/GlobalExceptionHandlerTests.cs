using FluentAssertions;
using Ekipzen.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ekipzen.Tests.Integration.Middleware;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task ArgumentException_IsMappedToGenericBadRequest()
    {
        var writer = new CapturingProblemDetailsService();
        var handler = new GlobalExceptionHandler(
            writer, NullLogger<GlobalExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            httpContext,
            new ArgumentException("İç uygulama ayrıntısı istemciye çıkmamalı."),
            CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        writer.Context.Should().NotBeNull();
        writer.Context!.ProblemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        writer.Context.ProblemDetails.Title.Should().Be("Geçersiz istek.");
        writer.Context.ProblemDetails.Detail.Should().BeNull();
    }

    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? Context { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Context = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Context = context;
            return ValueTask.FromResult(true);
        }
    }
}
