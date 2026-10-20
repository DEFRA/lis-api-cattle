// <copyright file="LoggableRequestPathTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Logging;

using Defra.Lis.Api.Logging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Serilog.Events;
using Serilog.Parsing;

public class LoggableRequestPathTests
{
    private const string Template = "/v{version:apiVersion}/users/{subject}";

    [Fact]
    public void For_ReturnsTheRouteTemplateOfTheMatchedEndpoint()
    {
        var context = RequestFor("/v1/users/subject-1");
        context.SetEndpoint(UsersEndpoint());

        Assert.Equal(Template, LoggableRequestPath.For(context));
    }

    [Fact]
    public void For_ReadsTheEndpointFromTheExceptionHandlerFeature_WhenTheEndpointWasCleared()
    {
        var context = RequestFor("/v1/users/subject-1");
        context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature
        {
            Error = new InvalidOperationException(),
            Endpoint = UsersEndpoint(),
            Path = "/v1/users/subject-1",
        });

        Assert.Equal(Template, LoggableRequestPath.For(context));
    }

    [Fact]
    public void For_ReturnsAPlaceholder_WhenNoRouteMatched()
    {
        var context = RequestFor("/v1/user/subject-1");

        Assert.Equal(LoggableRequestPath.UnmatchedRoute, LoggableRequestPath.For(context));
    }

    [Fact]
    public void Enricher_ReplacesTheRawRequestPathWithTheRouteTemplate()
    {
        var context = RequestFor("/v1/users/subject-1");
        context.SetEndpoint(UsersEndpoint());
        var logEvent = EventWith(new LogEventProperty(LoggableRequestPathEnricher.PropertyName, new ScalarValue("/v1/users/subject-1")));

        new LoggableRequestPathEnricher(new HttpContextAccessor { HttpContext = context }).Enrich(logEvent, new PropertyFactory());

        Assert.Equal(Template, ((ScalarValue)logEvent.Properties[LoggableRequestPathEnricher.PropertyName]).Value);
    }

    [Fact]
    public void Enricher_LeavesEventsOutsideARequestUnchanged()
    {
        var logEvent = EventWith();

        new LoggableRequestPathEnricher(new HttpContextAccessor()).Enrich(logEvent, new PropertyFactory());

        Assert.False(logEvent.Properties.ContainsKey(LoggableRequestPathEnricher.PropertyName));
    }

    private static DefaultHttpContext RequestFor(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context;
    }

    private static RouteEndpoint UsersEndpoint() =>
        new(_ => Task.CompletedTask, RoutePatternFactory.Parse(Template), 0, EndpointMetadataCollection.Empty, "users");

    private static LogEvent EventWith(params LogEventProperty[] properties) =>
        new(DateTimeOffset.UtcNow, LogEventLevel.Information, null, new MessageTemplateParser().Parse("test"), properties);

    private sealed class PropertyFactory : Serilog.Core.ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
