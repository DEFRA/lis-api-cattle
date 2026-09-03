// <copyright file="AwsMessagingConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Defra.Database.Postgres;
using Defra.Lis.Api.Messaging;
using Defra.Lis.Api.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public static class AwsMessagingConfiguration
{
    public static IServiceCollection AddAwsMessagingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AwsMessagingOptions>(configuration.GetSection(AwsMessagingOptions.SectionName));
        services.Configure<SubmissionValidationOptions>(configuration.GetSection(SubmissionValidationOptions.SectionName));

        var awsOptions = configuration.GetSection(AwsMessagingOptions.SectionName).Get<AwsMessagingOptions>() ?? new AwsMessagingOptions();

        services.AddSingleton<IAmazonSQS>(_ =>
        {
            var config = new AmazonSQSConfig();
            if (!string.IsNullOrWhiteSpace(awsOptions.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(awsOptions.Region);
            }

            if (awsOptions.UseLocalStack && !string.IsNullOrWhiteSpace(awsOptions.ServiceUrl))
            {
                config.ServiceURL = awsOptions.ServiceUrl;
            }

            return new AmazonSQSClient(config);
        });

        services.AddSingleton<IAmazonSimpleNotificationService>(_ =>
        {
            var config = new AmazonSimpleNotificationServiceConfig();
            if (!string.IsNullOrWhiteSpace(awsOptions.Region))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(awsOptions.Region);
            }

            if (awsOptions.UseLocalStack && !string.IsNullOrWhiteSpace(awsOptions.ServiceUrl))
            {
                config.ServiceURL = awsOptions.ServiceUrl;
            }

            return new AmazonSimpleNotificationServiceClient(config);
        });

        services.AddScoped<ISubmissionMessagePublisher, SubmissionMessagePublisher>();
        services.AddScoped<ISubmissionValidationService>(serviceProvider =>
            new SubmissionValidationService(
                serviceProvider.GetRequiredService<PostgresDbContext>(),
                serviceProvider.GetRequiredService<Defra.Lis.Api.Interfaces.ICadsService>(),
                serviceProvider.GetRequiredService<IOptions<SubmissionValidationOptions>>(),
                serviceProvider.GetRequiredService<ILogger<SubmissionValidationService>>()));
        services.AddScoped<ISubmissionValidationQueueProcessor, SubmissionValidationQueueProcessor>();

        if (awsOptions.EnableBackgroundConsumer)
        {
            services.AddHostedService<SubmissionValidationBackgroundService>();
        }

        return services;
    }
}
