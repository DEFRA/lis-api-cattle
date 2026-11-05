// <copyright file="CadsDataPatchConfigurationTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Amazon;
using Amazon.S3;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public class CadsDataPatchConfigurationTests
{
    [Fact]
    public void AddCadsDataPatch_WithLocalStack_PointsTheS3ClientAtLocalStackUsingPathStyle()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["AWS:Region"] = "eu-west-2",
            ["AWS:UseLocalStack"] = "true",
            ["AWS:ServiceUrl"] = "http://localhost:4566",
            ["CadsDataPatch:BucketName"] = "cads-data-patch",
        });

        var config = provider.GetRequiredService<IAmazonS3>().Config;

        Assert.StartsWith("http://localhost:4566", config.ServiceURL, StringComparison.Ordinal);
        Assert.Equal("eu-west-2", config.AuthenticationRegion);
        Assert.True(((AmazonS3Config)config).ForcePathStyle);
        Assert.Equal("cads-data-patch", provider.GetRequiredService<IOptions<CadsDataPatchOptions>>().Value.BucketName);
        Assert.IsType<CadsDataPatchStore>(provider.CreateScope().ServiceProvider.GetRequiredService<ICadsDataPatchStore>());
    }

    [Theory]
    [InlineData("false", "http://localhost:4566")]
    [InlineData("true", null)]
    public void AddCadsDataPatch_WithoutALocalStackUrl_UsesTheRegionalS3Endpoint(string useLocalStack, string? serviceUrl)
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["AWS:Region"] = "eu-west-2",
            ["AWS:UseLocalStack"] = useLocalStack,
            ["AWS:ServiceUrl"] = serviceUrl,
        });

        var config = provider.GetRequiredService<IAmazonS3>().Config;

        Assert.Equal(RegionEndpoint.EUWest2, config.RegionEndpoint);
        Assert.Null(config.ServiceURL);
        Assert.False(((AmazonS3Config)config).ForcePathStyle);
        Assert.False(provider.GetRequiredService<IOptions<CadsDataPatchOptions>>().Value.IsEnabled);
    }

    [Fact]
    public void AddCadsDataPatch_WithoutAnAwsSection_StillRegistersTheStore()
    {
        var services = new ServiceCollection();

        services.AddCadsDataPatch(new ConfigurationBuilder().Build());

        Assert.Contains(services, s => s.ServiceType == typeof(IAmazonS3) && s.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, s => s.ServiceType == typeof(ICadsDataPatchStore) && s.ImplementationType == typeof(CadsDataPatchStore) && s.Lifetime == ServiceLifetime.Scoped);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCadsDataPatch(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services.BuildServiceProvider();
    }
}
