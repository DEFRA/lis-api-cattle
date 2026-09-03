// <copyright file="AwsMessagingConfigurationTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Messaging;
using Defra.Lis.Api.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

public class AwsMessagingConfigurationTests
{
    [Fact]
    public void AddAwsMessagingServices_WithLocalStack_RegistersRequiredServices()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["AWS:Region"] = "eu-west-2",
            ["AWS:UseLocalStack"] = "true",
            ["AWS:ServiceUrl"] = "http://localhost:4566",
            ["AWS:SubmissionValidationQueueUrl"] = "http://localhost:4566/000000000000/submission_validation_queue",
            ["AWS:SubmissionValidationTopicArn"] = "arn:aws:sns:eu-west-2:000000000000:submission_validation_topic",
            ["AWS:EnableBackgroundConsumer"] = "true",
            ["SubmissionValidation:MinDamAgeInMonths"] = "15",
            ["SubmissionValidation:MaxDamAgeInYears"] = "20",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Interfaces.ICadsService, Services.CadsService>(_ =>
            new Services.CadsService(new HttpClient()));
        services.AddDbContext<Microsoft.EntityFrameworkCore.DbContext, Defra.Database.Postgres.PostgresDbContext>(options =>
            Microsoft.EntityFrameworkCore.InMemoryDbContextOptionsExtensions.UseInMemoryDatabase(options, "TestDb"));

        services.AddAwsMessagingServices(configuration);

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IAmazonSQS>());
        Assert.NotNull(provider.GetService<IAmazonSimpleNotificationService>());
        Assert.NotNull(provider.GetService<ISubmissionMessagePublisher>());
        Assert.NotNull(provider.GetService<ISubmissionValidationService>());
        Assert.NotNull(provider.GetService<ISubmissionValidationQueueProcessor>());
        Assert.Contains(services, s => s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(SubmissionValidationBackgroundService));
    }

    [Fact]
    public void AddAwsMessagingServices_WithoutLocalStack_RegistersRequiredServices()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["AWS:Region"] = "eu-west-2",
            ["AWS:UseLocalStack"] = "false",
            ["AWS:SubmissionValidationQueueUrl"] = "https://sqs.eu-west-2.amazonaws.com/332499610595/lis_cattle_submission_intake",
            ["AWS:EnableBackgroundConsumer"] = "true",
            ["SubmissionValidation:MinDamAgeInMonths"] = "15",
            ["SubmissionValidation:MaxDamAgeInYears"] = "20",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Interfaces.ICadsService, Services.CadsService>(_ =>
            new Services.CadsService(new HttpClient()));
        services.AddDbContext<Microsoft.EntityFrameworkCore.DbContext, Defra.Database.Postgres.PostgresDbContext>(options =>
            Microsoft.EntityFrameworkCore.InMemoryDbContextOptionsExtensions.UseInMemoryDatabase(options, "TestDb2"));

        services.AddAwsMessagingServices(configuration);

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IAmazonSQS>());
        Assert.NotNull(provider.GetService<IAmazonSimpleNotificationService>());
        Assert.NotNull(provider.GetService<ISubmissionMessagePublisher>());
        Assert.NotNull(provider.GetService<ISubmissionValidationService>());
        Assert.NotNull(provider.GetService<ISubmissionValidationQueueProcessor>());
    }

    [Fact]
    public void AppSettings_AwsConfiguration_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var awsOptions = configuration.GetSection(AwsMessagingOptions.SectionName).Get<AwsMessagingOptions>();

        Assert.NotNull(awsOptions);
        Assert.False(awsOptions.UseLocalStack);
        Assert.Equal("eu-west-2", awsOptions.Region);
        Assert.Null(awsOptions.ServiceUrl);
        Assert.True(awsOptions.EnableBackgroundConsumer);
    }

    [Fact]
    public void AppSettingsDevelopment_AwsConfiguration_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .Build();

        var awsOptions = configuration.GetSection(AwsMessagingOptions.SectionName).Get<AwsMessagingOptions>();

        Assert.NotNull(awsOptions);
        Assert.True(awsOptions.UseLocalStack);
        Assert.Equal("http://localhost:4566", awsOptions.ServiceUrl);
        Assert.Equal("http://localhost:4566/000000000000/submission_validation_queue", awsOptions.SubmissionValidationQueueUrl);
        Assert.Equal("arn:aws:sns:eu-west-2:000000000000:submission_validation_topic", awsOptions.SubmissionValidationTopicArn);
    }
}
