// <copyright file="CtsConfigurationTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class CtsConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    public void AddCtsServices_RegistersTheFake_WhenUseFakeIsTrueOrUnset(string? useFake)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddCtsServices(Configuration(useFake));

        using var provider = services.BuildServiceProvider();
        Assert.IsType<FakeCtsService>(provider.GetRequiredService<ICtsService>());
    }

    [Fact]
    public void AddCtsServices_FailsAtStartUp_WhenUseFakeIsFalse()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddCtsServices(Configuration("false")));

        Assert.Contains("CtsApi:UseFake", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ICtsService));
    }

    private static IConfiguration Configuration(string? useFake) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [CtsConfiguration.UseFakeKey] = useFake })
        .Build();
}
