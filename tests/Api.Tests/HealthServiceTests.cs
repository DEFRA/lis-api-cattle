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

public class HealthServiceTests : IDisposable
{
    private readonly Mock<ILogger<HealthService>> mockLogger = new();
    private readonly Mock<IAmazonSQS> mockSqs = new();
    private readonly Mock<ISchedulerFactory> mockSchedulerFactory = new();
    private readonly Mock<IScheduler> mockScheduler = new();
    private readonly PostgresDbContext defaultDbContext;

    public HealthServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        this.defaultDbContext = new PostgresDbContext(dbOptions);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new HealthService(
            null!,
            this.defaultDbContext,
            this.mockSqs.Object,
            this.mockSchedulerFactory.Object,
            Options.Create(new AwsMessagingOptions()),
            Options.Create(new CtsPollingJobOptions())));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenDbContextIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new HealthService(
            this.mockLogger.Object,
            null!,
            this.mockSqs.Object,
            this.mockSchedulerFactory.Object,
            Options.Create(new AwsMessagingOptions()),
            Options.Create(new CtsPollingJobOptions())));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenSqsClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new HealthService(
            this.mockLogger.Object,
            this.defaultDbContext,
            null!,
            this.mockSchedulerFactory.Object,
            Options.Create(new AwsMessagingOptions()),
            Options.Create(new CtsPollingJobOptions())));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenSchedulerFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new HealthService(
            this.mockLogger.Object,
            this.defaultDbContext,
            this.mockSqs.Object,
            null!,
            Options.Create(new AwsMessagingOptions()),
            Options.Create(new CtsPollingJobOptions())));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenAwsOptionsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new HealthService(
            this.mockLogger.Object,
            this.defaultDbContext,
            this.mockSqs.Object,
            this.mockSchedulerFactory.Object,
            null!,
            Options.Create(new CtsPollingJobOptions())));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenCtsJobOptionsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new HealthService(
            this.mockLogger.Object,
            this.defaultDbContext,
            this.mockSqs.Object,
            this.mockSchedulerFactory.Object,
            Options.Create(new AwsMessagingOptions()),
            null!));
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_WhenInMemoryDbContextCanConnect_ReturnsHealthy()
    {
        var service = this.CreateHealthService();

        var result = await service.CheckDatabaseHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Contains("healthy", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_WhenCanConnectThrowsException_ReturnsUnhealthy()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(
                "Host=nonexistent-host-for-health-test;Database=db;Username=u;Password=p;Timeout=1;CommandTimeout=1")
            .Options;

        await using var dbContext = new PostgresDbContext(options);
        var service = this.CreateHealthService(dbContext: dbContext);

        var result = await service.CheckDatabaseHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);

        Assert.True(
            result.Description?.Contains("Database health check failed", StringComparison.OrdinalIgnoreCase) == true ||
            result.Description?.Contains("Cannot connect", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_WhenTimedOut_ReturnsUnhealthy()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql("Host=192.0.2.1;Database=db;Username=u;Password=p;Timeout=10;CommandTimeout=10")
            .Options;

        await using var dbContext = new PostgresDbContext(options);
        var service = this.CreateHealthService(
            dbContext: dbContext,
            checkTimeout: TimeSpan.FromMilliseconds(20));

        var result = await service.CheckDatabaseHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);

        Assert.True(
            result.Description?.Contains("timed out", StringComparison.OrdinalIgnoreCase) == true ||
            result.Description?.Contains("failed", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenNoQueueUrlConfigured_SkipsCheckAndReturnsHealthy()
    {
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = string.Empty });

        var service = this.CreateHealthService(awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Contains("skipped", result.Description, StringComparison.OrdinalIgnoreCase);

        this.mockSqs.Verify(
            s => s.ListQueuesAsync(It.IsAny<ListQueuesRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenQueueUrlConfigured_AndGetQueueAttributesSucceeds_ReturnsHealthy()
    {
        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        var getAttributesResponse = new GetQueueAttributesResponse
        {
            Attributes =
                new Dictionary<string, string> { [QueueAttributeName.ApproximateNumberOfMessages] = "42", },
        };

        this.mockSqs.Setup(s => s.GetQueueAttributesAsync(
                It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == queueUrl),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getAttributesResponse);

        var service = this.CreateHealthService(awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
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
            UseLocalStack = true, SubmissionValidationQueueUrl = queueUrl,
        });

        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QueueDoesNotExistException("The specified queue does not exist."));

        this.mockSqs.Setup(s => s.GetQueueUrlAsync(It.IsAny<GetQueueUrlRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QueueDoesNotExistException("The specified queue does not exist."));

        var service = this.CreateHealthService(awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("The specified queue does not exist", result.Description);
        this.mockSqs.Verify(
            s => s.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenQueueUrlFailsWithQueueDoesNotExist_ResolvesByNameAndReturnsHealthy()
    {
        var wrongHostUrl = "http://localhost:4566/000000000000/submission-validation-queue";
        var resolvedUrl = "http://localstack:4566/000000000000/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions
        {
            UseLocalStack = false, SubmissionValidationQueueUrl = wrongHostUrl,
        });

        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(
                    It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == wrongHostUrl),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QueueDoesNotExistException("The specified queue does not exist."));

        this.mockSqs.Setup(s =>
                s.GetQueueUrlAsync(
                    It.Is<GetQueueUrlRequest>(r => r.QueueName == "submission-validation-queue"),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = resolvedUrl });

        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(
                    It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == resolvedUrl),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    [QueueAttributeName.ApproximateNumberOfMessages] = "5",
                },
            });

        var service = this.CreateHealthService(awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Equal(resolvedUrl, result.Data?["queueUrl"]);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenQueueNameOnlyConfigured_ResolvesUrlAndReturnsHealthy()
    {
        var queueName = "submission_validation_queue";
        var resolvedUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission_validation_queue";
        var awsOptions = Options.Create(new AwsMessagingOptions
        {
            UseLocalStack = false, SubmissionValidationQueueUrl = queueName,
        });

        this.mockSqs.Setup(s =>
                s.GetQueueUrlAsync(
                    It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = resolvedUrl });

        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(
                    It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == resolvedUrl),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    [QueueAttributeName.ApproximateNumberOfMessages] = "10",
                },
            });

        var service = this.CreateHealthService(awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Equal(resolvedUrl, result.Data?["queueUrl"]);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenSqsThrowsException_ReturnsUnhealthy()
    {
        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSQSException("Queue not accessible"));

        var service = this.CreateHealthService(awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("Queue not accessible", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAllComponentsAreHealthy_ReturnsHealthy()
    {
        const string queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
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

        var service = this.CreateHealthService(awsOptions: awsOptions);

        var healthCheckResponse = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Healthy), healthCheckResponse.Status);
        Assert.Equal(3, healthCheckResponse.Entries.Count);
        Assert.Equal(nameof(HealthStatus.Healthy), healthCheckResponse.Entries["database"].Status);
        Assert.Equal(nameof(HealthStatus.Healthy), healthCheckResponse.Entries["queue"].Status);
        Assert.Equal(nameof(HealthStatus.Healthy), healthCheckResponse.Entries["quartz"].Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenComponentIsUnhealthy_ReturnsUnhealthy()
    {
        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSQSException("Queue is down"));

        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(false);

        var service = this.CreateHealthService(awsOptions: awsOptions);

        var healthCheckResponse = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), healthCheckResponse.Status);
        Assert.Equal(nameof(HealthStatus.Healthy), healthCheckResponse.Entries["database"].Status);
        Assert.Equal(nameof(HealthStatus.Unhealthy), healthCheckResponse.Entries["queue"].Status);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenSqsTimesOut_ReturnsUnhealthy()
    {
        const string queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (GetQueueAttributesRequest _, CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                return new GetQueueAttributesResponse();
            });

        var service = this.CreateHealthService(
            awsOptions: awsOptions,
            checkTimeout: TimeSpan.FromMilliseconds(50));

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("timed out", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenSchedulerIsShutdown_ReturnsUnhealthy()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(true);
        this.mockScheduler.Setup(s => s.SchedulerName).Returns("TestScheduler");

        var service = this.CreateHealthService();

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("shutdown", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenJobEnabledAndRegisteredWithTrigger_ReturnsHealthy()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(false);
        this.mockScheduler.Setup(s =>
                s.CheckExists(It.Is<JobKey>(k => k.Name == nameof(CtsBundlePollingJob)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var mockTrigger = new Mock<ITrigger>();
        mockTrigger.Setup(t => t.Key).Returns(new TriggerKey("test-trigger"));
        var nextFire = DateTimeOffset.UtcNow.AddMinutes(5);
        mockTrigger.Setup(t => t.GetNextFireTimeUtc()).Returns(nextFire);
        this.mockScheduler.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([mockTrigger.Object]);
        this.mockScheduler.Setup(s => s.GetTriggerState(It.IsAny<TriggerKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TriggerState.Normal);

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
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

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
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

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
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

        var service = this.CreateHealthService(
            checkTimeout: TimeSpan.FromMilliseconds(50));

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("timed out", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenSchedulerFactoryReturnsNull_ReturnsUnhealthy()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IScheduler)null!);

        var service = this.CreateHealthService();

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Equal("Quartz scheduler instance could not be obtained.", result.Description);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenJobDisabled_ReturnsHealthy()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(false);
        this.mockScheduler.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        this.mockScheduler.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = false }));

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Contains("disabled by configuration", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenJobHasNoTriggers_ReturnsDegraded()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(false);
        this.mockScheduler.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockScheduler.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Degraded), result.Status);
        Assert.Contains("has no triggers scheduled", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenTriggerInPausedState_ReturnsDegraded()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(false);
        this.mockScheduler.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var mockTrigger = new Mock<ITrigger>();
        mockTrigger.Setup(t => t.Key).Returns(new TriggerKey("paused-trigger"));
        this.mockScheduler.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([mockTrigger.Object]);
        this.mockScheduler.Setup(s => s.GetTriggerState(It.IsAny<TriggerKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TriggerState.Paused);

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Degraded), result.Status);
        Assert.Contains("Paused", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenSchedulerThrowsException_ReturnsUnhealthy()
    {
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Scheduler crash"));

        var service = this.CreateHealthService();

        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("Scheduler crash", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenComponentIsDegraded_ReturnsDegradedOverall()
    {
        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });
        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse());

        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.IsShutdown).Returns(false);
        this.mockScheduler.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockScheduler.Setup(s => s.GetTriggersOfJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = this.CreateHealthService(
            awsOptions: awsOptions,
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        var response = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Degraded), response.Status);
        Assert.Equal(nameof(HealthStatus.Healthy), response.Entries["database"].Status);
        Assert.Equal(nameof(HealthStatus.Healthy), response.Entries["queue"].Status);
        Assert.Equal(nameof(HealthStatus.Degraded), response.Entries["quartz"].Status);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenAttributesDoNotContainMessageCount_DefaultsToZero()
    {
        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        var getAttributesResponse = new GetQueueAttributesResponse
        {
            Attributes = new Dictionary<string, string> { ["SomeOtherAttribute"] = "value", },
        };

        this.mockSqs.Setup(s => s.GetQueueAttributesAsync(
                It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == queueUrl),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getAttributesResponse);

        var service = this.CreateHealthService(awsOptions: awsOptions);

        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data["approximateNumberOfMessages"]);
    }

    public void Dispose()
    {
        this.defaultDbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private HealthService CreateHealthService(
        PostgresDbContext? dbContext = null,
        IAmazonSQS? sqsClient = null,
        ISchedulerFactory? schedulerFactory = null,
        IOptions<AwsMessagingOptions>? awsOptions = null,
        IOptions<CtsPollingJobOptions>? ctsJobOptions = null,
        TimeSpan? checkTimeout = null)
    {
        return new HealthService(
            this.mockLogger.Object,
            dbContext ?? this.defaultDbContext,
            sqsClient ?? this.mockSqs.Object,
            schedulerFactory ?? this.mockSchedulerFactory.Object,
            awsOptions ?? Options.Create(new AwsMessagingOptions()),
            ctsJobOptions ?? Options.Create(new CtsPollingJobOptions()),
            checkTimeout);
    }
}
