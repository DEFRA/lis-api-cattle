// <copyright file="ApiExceptionHandlerTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Correlation;

using System.Text.Json;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

public class ApiExceptionHandlerTests
{
    [Fact]
    public async Task ProblemDetails_CarryTheCorrelationId()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        context.Request.Headers[TraceHeaders.CdpRequestId] = "corr-598";
        context.Response.Body = new MemoryStream();
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, new NotFoundException("missing"), TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("corr-598", context.Response.Headers[TraceHeaders.CdpRequestId].ToString());
        context.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("corr-598", problem.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("trace-1", problem.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Logs_TheRouteTemplate_NotTheRawPath()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        context.Request.Path = "/v1/users/subject-751";
        context.Response.Body = new MemoryStream();
        context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature
        {
            Error = new NotFoundException("The user was not found."),
            Endpoint = new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/v{version:apiVersion}/users/{subject}"), 0, EndpointMetadataCollection.Empty, "users"),
            Path = "/v1/users/subject-751",
        });
        var sink = new CapturingSink();
        using var serilog = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog);
        var handler = new ApiExceptionHandler(factory.CreateLogger<ApiExceptionHandler>());

        await handler.TryHandleAsync(context, new NotFoundException("The user was not found."), TestContext.Current.CancellationToken);

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal("/v{version:apiVersion}/users/{subject}", ((ScalarValue)logEvent.Properties["Path"]).Value);
        Assert.DoesNotContain("subject-751", logEvent.RenderMessage(), StringComparison.Ordinal);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
