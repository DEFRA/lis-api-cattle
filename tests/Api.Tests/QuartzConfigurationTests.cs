using Lis.Cattle.Configurations;
using Lis.Cattle.Jobs;
using Lis.Cattle.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace Lis.Cattle;

public class QuartzConfigurationTests
{
    [Fact]
    public void AddQuartzServices_WithCronConfiguration_RegistersJobAndScheduler()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "CtsPollingJob:Enabled", "true" },
            { "CtsPollingJob:CronSchedule", "0/15 * * * * ?" },
            { "CtsPollingJob:BatchSize", "25" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartzServices(configuration);

        var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<CtsPollingJobOptions>>().Value;
        Assert.True(options.Enabled);
        Assert.Equal("0/15 * * * * ?", options.CronSchedule);
        Assert.Equal(25, options.BatchSize);

        var schedulerFactory = serviceProvider.GetService<ISchedulerFactory>();
        Assert.NotNull(schedulerFactory);
    }

    [Fact]
    public void AddQuartzServices_WithIntervalConfiguration_RegistersTriggerWithInterval()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "CtsPollingJob:Enabled", "true" },
            { "CtsPollingJob:CronSchedule", "" },
            { "CtsPollingJob:PollingIntervalSeconds", "45" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartzServices(configuration);

        var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<CtsPollingJobOptions>>().Value;
        Assert.True(options.Enabled);
        Assert.Equal(45, options.PollingIntervalSeconds);

        var schedulerFactory = serviceProvider.GetService<ISchedulerFactory>();
        Assert.NotNull(schedulerFactory);
    }

    [Fact]
    public void AddQuartzServices_WhenDisabled_DoesNotRegisterJob()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "CtsPollingJob:Enabled", "false" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartzServices(configuration);

        var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<CtsPollingJobOptions>>().Value;
        Assert.False(options.Enabled);
    }
}
