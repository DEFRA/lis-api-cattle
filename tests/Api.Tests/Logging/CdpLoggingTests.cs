// <copyright file="CdpLoggingTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Logging;

using Defra.Lis.Api.Configurations;
using Defra.Livestock.Sdk.Api.Strategies.Operations.Http.Rest.Client;
using Defra.Livestock.Sdk.Api.Strategies.Operations.Http.Soap.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

public class CdpLoggingTests
{
    [Fact]
    public void RawUrlLoggers_ContainsSoapHttpClient()
    {
        Assert.Contains(typeof(SoapHttpClient).FullName!, CdpLogging.RawUrlLoggers);
        Assert.Contains(typeof(RestHttpClient).FullName!, CdpLogging.RawUrlLoggers);
        Assert.Contains("Microsoft.AspNetCore.Hosting.Diagnostics", CdpLogging.RawUrlLoggers);
        Assert.Contains("System.Net.Http.HttpClient", CdpLogging.RawUrlLoggers);
    }

    [Fact]
    public void Configuration_KeepsRawUrlLoggersAtWarning_EvenWhenSettingsLowerThem()
    {
        var settings = new Dictionary<string, string?> { ["Serilog:MinimumLevel:Default"] = "Verbose" };

        foreach (var source in CdpLogging.RawUrlLoggers)
        {
            settings[$"Serilog:MinimumLevel:Override:{source}"] = "Verbose";
        }

        var context = new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
        };

        using var services = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
        var configuration = new LoggerConfiguration();

        CdpLogging.Configuration(context, services, configuration);

        using var logger = configuration.CreateLogger();

        Assert.True(logger
            .ForContext(Serilog.Core.Constants.SourceContextPropertyName, "Defra.Lis.Api.Services.KrdsService")
            .IsEnabled(LogEventLevel.Verbose));

        Assert.All(CdpLogging.RawUrlLoggers, source =>
        {
            var categoryLogger = logger.ForContext(Serilog.Core.Constants.SourceContextPropertyName, $"{source}.Child");
            Assert.False(categoryLogger.IsEnabled(LogEventLevel.Information));
            Assert.True(categoryLogger.IsEnabled(LogEventLevel.Warning));
        });
    }

    [Fact]
    public void Configuration_OverridesSoapHttpClientLoggingToWarning()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Serilog:MinimumLevel:Default"] = "Verbose",
            [$"Serilog:MinimumLevel:Override:{typeof(SoapHttpClient).FullName}"] = "Verbose",
        };

        var context = new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
        };

        using var services = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();

        var configuration = new LoggerConfiguration();

        CdpLogging.Configuration(context, services, configuration);

        using var logger = configuration.CreateLogger();

        var soapLogger = logger.ForContext(
            Serilog.Core.Constants.SourceContextPropertyName,
            typeof(SoapHttpClient).FullName!);

        Assert.False(soapLogger.IsEnabled(LogEventLevel.Information));
        Assert.True(soapLogger.IsEnabled(LogEventLevel.Warning));
    }
}
