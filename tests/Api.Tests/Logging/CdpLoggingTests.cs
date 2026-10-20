// <copyright file="CdpLoggingTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Logging;

using Defra.Lis.Api.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

public class CdpLoggingTests
{
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

        Assert.True(logger.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "Defra.Lis.Api.Services.KrdsService").IsEnabled(LogEventLevel.Verbose));
        Assert.All(CdpLogging.RawUrlLoggers, source =>
        {
            var categoryLogger = logger.ForContext(Serilog.Core.Constants.SourceContextPropertyName, $"{source}.Child");
            Assert.False(categoryLogger.IsEnabled(LogEventLevel.Information));
            Assert.True(categoryLogger.IsEnabled(LogEventLevel.Warning));
        });
    }
}
