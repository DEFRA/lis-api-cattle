// <copyright file="ApiExceptionHandlerTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Exceptions;

using System.Text.Json;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Exceptions;
using Defra.Lis.Core.Exceptions;
using Defra.Livestock.Sdk.Api.Strategies.Abstractions.Exceptions;
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
    public static TheoryData<Type, int, string, string> ExceptionMappings => new()
    {
        {
            typeof(NotFoundException), StatusCodes.Status404NotFound, "Not Found", "https://httpstatuses.com/404"
        },
        {
            typeof(EntityNotFoundException), StatusCodes.Status404NotFound, "Not Found",
            "https://httpstatuses.com/404"
        },
        {
            typeof(ExistenceRuleException), StatusCodes.Status404NotFound, "Not Found",
            "https://httpstatuses.com/404"
        },
        {
            typeof(ConflictException), StatusCodes.Status409Conflict, "Conflict",
            "https://httpstatuses.com/409"
        },
        {
            typeof(ConflictRuleException), StatusCodes.Status409Conflict, "Conflict",
            "https://httpstatuses.com/409"
        },
        {
            typeof(BusinessRuleException), StatusCodes.Status400BadRequest, "Bad Request",
            "https://httpstatuses.com/400"
        },
        {
            typeof(ReferenceRuleException), StatusCodes.Status400BadRequest, "Bad Request",
            "https://httpstatuses.com/400"
        },
        {
            typeof(RequestValidationException), StatusCodes.Status400BadRequest, "Bad Request",
            "https://httpstatuses.com/400"
        },
        {
            typeof(ArgumentException), StatusCodes.Status400BadRequest, "Bad Request",
            "https://httpstatuses.com/400"
        },
        {
            typeof(UnauthorizedAccessException), StatusCodes.Status403Forbidden, "Forbidden",
            "https://httpstatuses.com/403"
        },
        {
            typeof(ServiceUnavailableException), StatusCodes.Status503ServiceUnavailable,
            "Service Unavailable", "https://httpstatuses.com/503"
        },
        {
            typeof(CircuitBreakerOpenException), StatusCodes.Status503ServiceUnavailable,
            "Service Unavailable", "https://httpstatuses.com/503"
        },
        {
            typeof(InvalidOperationException), StatusCodes.Status500InternalServerError,
            "Internal Server Error", "https://httpstatuses.com/500"
        },
    };

    [Theory]
    [MemberData(nameof(ExceptionMappings))]
    public async Task TryHandleAsync_MapsExceptionToExpectedProblemDetails(
        Type exceptionType,
        int expectedStatusCode,
        string expectedTitle,
        string expectedType)
    {
        // Arrange
        const string detail = "An error occurred.";
        var exception = (Exception)Activator.CreateInstance(exceptionType, detail)!;
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-123",
            Request = { Path = "/v1/test/path", Headers = { [TraceHeaders.CdpRequestId] = "corr-456" } },
            Response = { Body = new MemoryStream() },
        };

        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        // Act
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
        Assert.Equal("corr-456", context.Response.Headers[TraceHeaders.CdpRequestId].ToString());

        context.Response.Body.Position = 0;

        using var problem = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(expectedType, problem.RootElement.GetProperty("type").GetString());
        Assert.Equal(detail, problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal("/v1/test/path", problem.RootElement.GetProperty("instance").GetString());
        Assert.Equal("corr-456", problem.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("trace-123", problem.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_OmitsCorrelationIdHeader_WhenHeaderIsNotPresent()
    {
        // Arrange
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1", Response = { Body = new MemoryStream() } };
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        // Act
        var handled = await handler.TryHandleAsync(
            context,
            new NotFoundException("missing"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.False(context.Response.Headers.ContainsKey(TraceHeaders.CdpRequestId));

        context.Response.Body.Position = 0;

        using var problem = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, problem.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task ProblemDetails_CarryTheCorrelationId()
    {
        // Arrange
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };

        context.Request.Headers[TraceHeaders.CdpRequestId] = "corr-598";
        context.Response.Body = new MemoryStream();

        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        // Act
        var handled = await handler.TryHandleAsync(
            context,
            new NotFoundException("missing"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("corr-598", context.Response.Headers[TraceHeaders.CdpRequestId].ToString());

        context.Response.Body.Position = 0;

        using var problem = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("corr-598", problem.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("trace-1", problem.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_MapsCircuitBreakerOpenException_ToServiceUnavailableProblemDetails()
    {
        // Arrange
        const string detail = "Circuit breaker is open.";
        var exception = new CircuitBreakerOpenException(detail);
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-cb",
            Request = { Path = "/v1/test/cb", Headers = { [TraceHeaders.CdpRequestId] = "corr-cb" } },
            Response = { Body = new MemoryStream() },
        };

        context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature
        {
            Error = exception,
            Endpoint = new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse("/v{version:apiVersion}/test/cb"),
                0,
                EndpointMetadataCollection.Empty,
                "cb"),
            Path = "/v1/test/cb",
        });

        var sink = new CapturingSink();
        await using var serilog = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog);

        var handler = new ApiExceptionHandler(factory.CreateLogger<ApiExceptionHandler>());

        // Act
        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("corr-cb", context.Response.Headers[TraceHeaders.CdpRequestId].ToString());

        context.Response.Body.Position = 0;

        using var problem = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Service Unavailable", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("https://httpstatuses.com/503", problem.RootElement.GetProperty("type").GetString());
        Assert.Equal(detail, problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal("/v1/test/cb", problem.RootElement.GetProperty("instance").GetString());
        Assert.Equal("corr-cb", problem.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("trace-cb", problem.RootElement.GetProperty("traceId").GetString());

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Error, logEvent.Level);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, ((ScalarValue)logEvent.Properties["StatusCode"]).Value);
        Assert.Equal("corr-cb", ((ScalarValue)logEvent.Properties["CorrelationId"]).Value);
        Assert.Equal("trace-cb", ((ScalarValue)logEvent.Properties["TraceId"]).Value);
        Assert.Equal("/v{version:apiVersion}/test/cb", ((ScalarValue)logEvent.Properties["Path"]).Value);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, LogEventLevel.Warning)]
    [InlineData(StatusCodes.Status403Forbidden, LogEventLevel.Warning)]
    [InlineData(StatusCodes.Status404NotFound, LogEventLevel.Warning)]
    [InlineData(StatusCodes.Status409Conflict, LogEventLevel.Warning)]
    [InlineData(StatusCodes.Status500InternalServerError, LogEventLevel.Error)]
    [InlineData(StatusCodes.Status503ServiceUnavailable, LogEventLevel.Error)]
    public async Task Logs_AtExpectedLevelAndPushesContextProperties(int statusCode, LogEventLevel expectedLevel)
    {
        // Arrange
        Exception exception = statusCode switch
        {
            StatusCodes.Status400BadRequest => new BusinessRuleException("bad request"),
            StatusCodes.Status403Forbidden => new UnauthorizedAccessException("forbidden"),
            StatusCodes.Status404NotFound => new NotFoundException("not found"),
            StatusCodes.Status409Conflict => new ConflictException("conflict"),
            StatusCodes.Status500InternalServerError => new InvalidOperationException("server error"),
            StatusCodes.Status503ServiceUnavailable => new ServiceUnavailableException("service unavailable"),
            _ => throw new ArgumentOutOfRangeException(nameof(statusCode)),
        };

        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-999",
            Request =
            {
                Method = "POST",
                Path = "/v1/users/subject-751",
                Headers = { [TraceHeaders.CdpRequestId] = "corr-999" },
            },
            Response = { Body = new MemoryStream() },
        };

        context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature
        {
            Error = exception,
            Endpoint = new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse("/v{version:apiVersion}/users/{subject}"),
                0,
                EndpointMetadataCollection.Empty,
                "users"),
            Path = "/v1/users/subject-751",
        });

        var sink = new CapturingSink();

        await using var serilog = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog);

        var handler = new ApiExceptionHandler(factory.CreateLogger<ApiExceptionHandler>());

        // Act
        await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(expectedLevel, logEvent.Level);
        Assert.Equal(statusCode, ((ScalarValue)logEvent.Properties["StatusCode"]).Value);
        Assert.Equal("corr-999", ((ScalarValue)logEvent.Properties["CorrelationId"]).Value);
        Assert.Equal("trace-999", ((ScalarValue)logEvent.Properties["TraceId"]).Value);
        Assert.Equal("/v{version:apiVersion}/users/{subject}", ((ScalarValue)logEvent.Properties["Path"]).Value);
        Assert.DoesNotContain("subject-751", logEvent.RenderMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logs_TheRouteTemplate_NotTheRawPath()
    {
        // Arrange
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-1",
            Request = { Path = "/v1/users/subject-751" },
            Response = { Body = new MemoryStream() },
        };

        context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature
        {
            Error = new NotFoundException("The user was not found."),
            Endpoint = new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse("/v{version:apiVersion}/users/{subject}"),
                0,
                EndpointMetadataCollection.Empty,
                "users"),
            Path = "/v1/users/subject-751",
        });

        var sink = new CapturingSink();

        await using var serilog = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        using var factory = new SerilogLoggerFactory(serilog);

        var handler = new ApiExceptionHandler(factory.CreateLogger<ApiExceptionHandler>());

        // Act
        await handler.TryHandleAsync(
            context,
            new NotFoundException("The user was not found."),
            TestContext.Current.CancellationToken);

        // Assert
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
