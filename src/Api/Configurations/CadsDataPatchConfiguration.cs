// <copyright file="CadsDataPatchConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using Amazon;
using Amazon.S3;
using Defra.Lis.Api.Interfaces;
using Defra.Lis.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class CadsDataPatchConfiguration
{
    public static IServiceCollection AddCadsDataPatch(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CadsDataPatchOptions>(configuration.GetSection(CadsDataPatchOptions.SectionName));

        var awsOptions = configuration.GetSection(AwsMessagingOptions.SectionName).Get<AwsMessagingOptions>() ?? new AwsMessagingOptions();

        // Created lazily, so the client is only built when a holding's cattle are first read.
        services.AddSingleton<IAmazonS3>(_ =>
        {
            var config = new AmazonS3Config();
            if (!string.IsNullOrWhiteSpace(awsOptions.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(awsOptions.Region);
            }

            if (awsOptions.UseLocalStack && !string.IsNullOrWhiteSpace(awsOptions.ServiceUrl))
            {
                // Setting ServiceURL clears RegionEndpoint, so the SDK would otherwise sign for us-east-1.
                config.ServiceURL = awsOptions.ServiceUrl;
                config.AuthenticationRegion = awsOptions.Region;
                config.ForcePathStyle = true;
            }

            return new AmazonS3Client(config);
        });

        services.AddScoped<ICadsDataPatchStore, CadsDataPatchStore>();

        return services;
    }
}
