// <copyright file="CtsConfigurationTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Cts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class CtsConfigurationTests
{
    [Fact]
    public void AddCtsServices_RegistersTheCtsWebClientAndAClock()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddCtsServices(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        Assert.IsType<CtsWebClient>(provider.GetRequiredService<ICtsWebClient>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void AddCtsServices_KeepsAClockThatIsAlreadyRegistered()
    {
        var services = new ServiceCollection();
        var clock = new FixedClock();
        services.AddSingleton<TimeProvider>(clock);

        services.AddCtsServices(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
    }

    private sealed class FixedClock : TimeProvider;
}
