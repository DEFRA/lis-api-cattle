// <copyright file="HealthServiceTests.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests;

using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Database.Postgres;
using Defra.Lis.Api.Configurations;
using Defra.Lis.Api.Jobs;
using Defra.Lis.Api.Models;
using Defra.Lis.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;

public class HealthServiceTests
{
    private readonly Mock<ILogger<HealthService>> mockLogger = new();
    private readonly Mock<IAmazonSQS> mockSqs = new();
    private readonly Mock<ISchedulerFactory> mockSchedulerFactory = new();
    private readonly Mock<IScheduler> mockScheduler = new();

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new HealthService(null!));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_WhenDbContextIsNull_ReturnsUnhealthy()
    {
        var service = new HealthService(this.mockLogger.Object, dbContext: null);

        var result = await service.CheckDatabaseHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
        Assert.Equal("Database context is not configured.", result.Description);
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_WhenInMemoryDbContextCanConnect_ReturnsHealthy()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new PostgresDbContext(options);
        var service = new HealthService(this.mockLogger.Object, dbContext: dbContext);

        var result = await service.CheckDatabaseHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy.ToString(), result.Status);
        Assert.Contains("healthy", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenSqsClientIsNull_ReturnsUnhealthy()
    {
        var service = new HealthService(this.mockLogger.Object, sqsClient: null);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
        Assert.Equal("SQS client is not configured.", result.Description);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenNoQueueUrlConfigured_SkipsCheckAndReturnsHealthy()
    {
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = string.Empty });

        var service = new HealthService(
            this.mockLogger.Object,
            sqsClient: this.mockSqs.Object,
            awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy.ToString(), result.Status);
        Assert.Contains("skipped", result.Description, StringComparison.OrdinalIgnoreCase);
        this.mockSqs.Verify(s => s.ListQueuesAsync(It.IsAny<ListQueuesRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenQueueUrlConfigured_AndGetQueueAttributesSucceeds_ReturnsHealthy()
    {
        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        var getAttributesResponse = new GetQueueAttributesResponse
        {
            Attributes = new Dictionary<string, string>
            {
                [QueueAttributeName.ApproximateNumberOfMessages] = "42",
            },
        };

        this.mockSqs.Setup(s => s.GetQueueAttributesAsync(
                It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == queueUrl),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getAttributesResponse);

        var service = new HealthService(
            this.mockLogger.Object,
            sqsClient: this.mockSqs.Object,
            awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy.ToString(), result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(queueUrl, result.Data["queueUrl"]);
        Assert.Equal(42, result.Data["approximateNumberOfMessages"]);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenQueueDoesNotExist_ReturnsUnhealthyWithoutCreatingQueue()
    {
        var queueUrl = "http://localhost:4566/000000000000/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions
        {
            UseLocalStack = true,
            SubmissionValidationQueueUrl = queueUrl,
        });

        this.mockSqs.Setup(s => s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QueueDoesNotExistException("The specified queue does not exist."));

        this.mockSqs.Setup(s => s.GetQueueUrlAsync(It.IsAny<GetQueueUrlRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QueueDoesNotExistException("The specified queue does not exist."));

        var service = new HealthService(
            this.mockLogger.Object,
            sqsClient: this.mockSqs.Object,
            awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
        Assert.Contains("The specified queue does not exist", result.Description);
        this.mockSqs.Verify(s => s.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenQueueUrlFailsWithQueueDoesNotExist_ResolvesByNameAndReturnsHealthy()
    {
        var wrongHostUrl = "http://localhost:4566/000000000000/submission-validation-queue";
        var resolvedUrl = "http://localstack:4566/000000000000/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions
        {
            UseLocalStack = false,
            SubmissionValidationQueueUrl = wrongHostUrl,
        });

        this.mockSqs.Setup(s => s.GetQueueAttributesAsync(It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == wrongHostUrl), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QueueDoesNotExistException("The specified queue does not exist."));

        this.mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == "submission-validation-queue"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = resolvedUrl });

        this.mockSqs.Setup(s => s.GetQueueAttributesAsync(It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == resolvedUrl), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    [QueueAttributeName.ApproximateNumberOfMessages] = "5",
                },
            });

        var service = new HealthService(
            this.mockLogger.Object,
            sqsClient: this.mockSqs.Object,
            awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy.ToString(), result.Status);
        Assert.Equal(resolvedUrl, result.Data?["queueUrl"]);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenQueueNameOnlyConfigured_ResolvesUrlAndReturnsHealthy()
    {
        var queueName = "submission_validation_queue";
        var resolvedUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission_validation_queue";
        var awsOptions = Options.Create(new AwsMessagingOptions
        {
            UseLocalStack = false,
            SubmissionValidationQueueUrl = queueName,
        });

        this.mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = resolvedUrl });

        this.mockSqs.Setup(s => s.GetQueueAttributesAsync(It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == resolvedUrl), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    [QueueAttributeName.ApproximateNumberOfMessages] = "10",
                },
            });

        var service = new HealthService(
            this.mockLogger.Object,
            sqsClient: this.mockSqs.Object,
            awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy.ToString(), result.Status);
        Assert.Equal(resolvedUrl, result.Data?["queueUrl"]);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenSqsThrowsException_ReturnsUnhealthy()
    {
        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        this.mockSqs.Setup(s => s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSQSException("Queue not accessible"));

        var service = new HealthService(
            this.mockLogger.Object,
            sqsClient: this.mockSqs.Object,
            awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
        Assert.Contains("Queue not accessible", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAllComponentsAreHealthy_ReturnsHealthy()
    {
        var dbOptions = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new PostgresDbContext(dbOptions);

        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        this.mockSqs.Setup(s => s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse());

        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(false);
        this.mockScheduler.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var mockTrigger = new Mock<ITrigger>();
        mockTrigger.Setup(t => t.Key).Returns(new TriggerKey("test-trigger"));
        mockTrigger.Setup(t => t.GetNextFireTimeUtc()).Returns(DateTimeOffset.UtcNow.AddMinutes(1));
        this.mockScheduler.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([mockTrigger.Object]);
        this.mockScheduler.Setup(s => s.GetTriggerState(It.IsAny<TriggerKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TriggerState.Normal);

        var service = new HealthService(
            this.mockLogger.Object,
            dbContext: dbContext,
            sqsClient: this.mockSqs.Object,
            schedulerFactory: this.mockSchedulerFactory.Object,
            awsOptions: awsOptions);

        var healthCheckResponse = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy.ToString(), healthCheckResponse.Status);
        Assert.Equal(3, healthCheckResponse.Entries.Count);
        Assert.Equal(HealthStatus.Healthy.ToString(), healthCheckResponse.Entries["database"].Status);
        Assert.Equal(HealthStatus.Healthy.ToString(), healthCheckResponse.Entries["queue"].Status);
        Assert.Equal(HealthStatus.Healthy.ToString(), healthCheckResponse.Entries["quartz"].Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenComponentIsUnhealthy_ReturnsUnhealthy()
    {
        var dbOptions = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new PostgresDbContext(dbOptions);

        var service = new HealthService(
            this.mockLogger.Object,
            dbContext: dbContext,
            sqsClient: null);

        var healthCheckResponse = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy.ToString(), healthCheckResponse.Status);
        Assert.Equal(HealthStatus.Healthy.ToString(), healthCheckResponse.Entries["database"].Status);
        Assert.Equal(HealthStatus.Unhealthy.ToString(), healthCheckResponse.Entries["queue"].Status);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenSqsTimesOut_ReturnsUnhealthy()
    {
        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        this.mockSqs.Setup(s => s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (GetQueueAttributesRequest req, CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                return new GetQueueAttributesResponse();
            });

        var service = new HealthService(
            this.mockLogger.Object,
            sqsClient: this.mockSqs.Object,
            awsOptions: awsOptions,
            checkTimeout: TimeSpan.FromMilliseconds(50));

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
        Assert.Contains("timed out", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenSchedulerFactoryIsNull_ReturnsUnhealthy()
    {
        var service = new HealthService(this.mockLogger.Object, schedulerFactory: null);

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
        Assert.Equal("Quartz scheduler factory is not configured.", result.Description);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenSchedulerIsShutdown_ReturnsUnhealthy()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(true);
        this.mockScheduler.Setup(s => s.SchedulerName).Returns("TestScheduler");

        var service = new HealthService(this.mockLogger.Object, schedulerFactory: this.mockSchedulerFactory.Object);

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
        Assert.Contains("shutdown", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenJobEnabledAndRegisteredWithTrigger_ReturnsHealthy()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(false);
        this.mockScheduler.Setup(s => s.CheckExists(It.Is<JobKey>(k => k.Name == nameof(CtsBundlePollingJob)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var mockTrigger = new Mock<ITrigger>();
        mockTrigger.Setup(t => t.Key).Returns(new TriggerKey("test-trigger"));
        var nextFire = DateTimeOffset.UtcNow.AddMinutes(5);
        mockTrigger.Setup(t => t.GetNextFireTimeUtc()).Returns(nextFire);
        this.mockScheduler.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([mockTrigger.Object]);
        this.mockScheduler.Setup(s => s.GetTriggerState(It.IsAny<TriggerKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TriggerState.Normal);

        var service = new HealthService(
            this.mockLogger.Object,
            schedulerFactory: this.mockSchedulerFactory.Object,
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy.ToString(), result.Status);
        Assert.Contains("scheduled", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Data);
        Assert.Equal(nextFire.ToString("o"), result.Data["nextFireTimeUtc"]);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenJobEnabledButNotRegistered_ReturnsUnhealthy()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(false);
        this.mockScheduler.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        this.mockScheduler.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new HealthService(
            this.mockLogger.Object,
            schedulerFactory: this.mockSchedulerFactory.Object,
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
        Assert.Contains("not registered", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenTriggerInErrorState_ReturnsUnhealthy()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(false);
        this.mockScheduler.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var mockTrigger = new Mock<ITrigger>();
        mockTrigger.Setup(t => t.Key).Returns(new TriggerKey("error-trigger"));
        this.mockScheduler.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([mockTrigger.Object]);
        this.mockScheduler.Setup(s => s.GetTriggerState(It.IsAny<TriggerKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TriggerState.Error);

        var service = new HealthService(
            this.mockLogger.Object,
            schedulerFactory: this.mockSchedulerFactory.Object,
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
        Assert.Contains("Error state", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenSchedulerTimesOut_ReturnsUnhealthy()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                return this.mockScheduler.Object;
            });

        var service = new HealthService(
            this.mockLogger.Object,
            schedulerFactory: this.mockSchedulerFactory.Object,
            checkTimeout: TimeSpan.FromMilliseconds(50));

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy.ToString(), result.Status);
        Assert.Contains("timed out", result.Description, StringComparison.OrdinalIgnoreCase);
    }
}
