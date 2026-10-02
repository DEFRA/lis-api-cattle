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

public sealed class HealthServiceTests : IDisposable
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
        // Arrange
        ILogger<HealthService> logger = null!;

        // Act
        var act = () => new HealthService(
            logger,
            this.defaultDbContext,
            this.mockSqs.Object,
            this.mockSchedulerFactory.Object,
            Options.Create(new AwsMessagingOptions()),
            Options.Create(new CtsPollingJobOptions()));

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenDbContextIsNull()
    {
        // Arrange
        PostgresDbContext dbContext = null!;

        // Act
        var act = () => new HealthService(
            this.mockLogger.Object,
            dbContext,
            this.mockSqs.Object,
            this.mockSchedulerFactory.Object,
            Options.Create(new AwsMessagingOptions()),
            Options.Create(new CtsPollingJobOptions()));

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenSqsClientIsNull()
    {
        // Arrange
        IAmazonSQS sqsClient = null!;

        // Act
        var act = () => new HealthService(
            this.mockLogger.Object,
            this.defaultDbContext,
            sqsClient,
            this.mockSchedulerFactory.Object,
            Options.Create(new AwsMessagingOptions()),
            Options.Create(new CtsPollingJobOptions()));

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenSchedulerFactoryIsNull()
    {
        // Arrange
        ISchedulerFactory schedulerFactory = null!;

        // Act
        var act = () => new HealthService(
            this.mockLogger.Object,
            this.defaultDbContext,
            this.mockSqs.Object,
            schedulerFactory,
            Options.Create(new AwsMessagingOptions()),
            Options.Create(new CtsPollingJobOptions()));

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenAwsOptionsIsNull()
    {
        // Arrange
        IOptions<AwsMessagingOptions> awsOptions = null!;

        // Act
        var act = () => new HealthService(
            this.mockLogger.Object,
            this.defaultDbContext,
            this.mockSqs.Object,
            this.mockSchedulerFactory.Object,
            awsOptions,
            Options.Create(new CtsPollingJobOptions()));

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenCtsJobOptionsIsNull()
    {
        // Arrange
        IOptions<CtsPollingJobOptions> ctsJobOptions = null!;

        // Act
        var act = () => new HealthService(
            this.mockLogger.Object,
            this.defaultDbContext,
            this.mockSqs.Object,
            this.mockSchedulerFactory.Object,
            Options.Create(new AwsMessagingOptions()),
            ctsJobOptions);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_WhenInMemoryDbContextCanConnect_ReturnsHealthy()
    {
        // Arrange
        var service = this.CreateHealthService();

        // Act
        var result = await service.CheckDatabaseHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Contains("healthy", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_WhenCanConnectThrowsException_ReturnsUnhealthy()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(
                "Host=nonexistent-host-for-health-test;Database=db;Username=u;Password=p;Timeout=1;CommandTimeout=1")
            .Options;

        await using var dbContext = new PostgresDbContext(options);
        var service = this.CreateHealthService(dbContext: dbContext);

        // Act
        var result = await service.CheckDatabaseHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);

        Assert.True(
            result.Description?.Contains("Database health check failed", StringComparison.OrdinalIgnoreCase) == true ||
            result.Description?.Contains("Cannot connect", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task CheckDatabaseHealthAsync_WhenTimedOut_ReturnsUnhealthy()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql("Host=192.0.2.1;Database=db;Username=u;Password=p;Timeout=10;CommandTimeout=10")
            .Options;

        await using var dbContext = new PostgresDbContext(options);
        var service = this.CreateHealthService(
            dbContext: dbContext,
            checkTimeout: TimeSpan.FromMilliseconds(20));

        // Act
        var result = await service.CheckDatabaseHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);

        Assert.True(
            result.Description?.Contains("timed out", StringComparison.OrdinalIgnoreCase) == true ||
            result.Description?.Contains("failed", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenNoQueueUrlConfigured_SkipsCheckAndReturnsHealthy()
    {
        // Arrange
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = string.Empty });

        var service = this.CreateHealthService(awsOptions: awsOptions);

        // Act
        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Contains("skipped", result.Description, StringComparison.OrdinalIgnoreCase);

        this.mockSqs.Verify(
            s => s.ListQueuesAsync(It.IsAny<ListQueuesRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenQueueUrlConfigured_AndGetQueueAttributesSucceeds_ReturnsHealthy()
    {
        // Arrange
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

        // Act
        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(queueUrl, result.Data["queueUrl"]);
        Assert.Equal(42, result.Data["approximateNumberOfMessages"]);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenQueueDoesNotExist_ReturnsUnhealthyWithoutCreatingQueue()
    {
        // Arrange
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

        // Act
        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("The specified queue does not exist", result.Description);
        this.mockSqs.Verify(
            s => s.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenQueueUrlFailsWithQueueDoesNotExist_ResolvesByNameAndReturnsHealthy()
    {
        // Arrange
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

        // Act
        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Equal(resolvedUrl, result.Data?["queueUrl"]);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenQueueNameOnlyConfigured_ResolvesUrlAndReturnsHealthy()
    {
        // Arrange
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

        // Act
        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Equal(resolvedUrl, result.Data?["queueUrl"]);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenSqsThrowsException_ReturnsUnhealthy()
    {
        // Arrange
        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSQSException("Queue not accessible"));

        var service = this.CreateHealthService(awsOptions: awsOptions);

        // Act
        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("Queue not accessible", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAllComponentsAreHealthy_ReturnsHealthy()
    {
        // Arrange
        const string queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse());

        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.Status).Returns(SchedulerStatus.Running);
        this.mockScheduler.Setup(s => s.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var header = CreateTriggerHeader(
            new TriggerKey("test-trigger"),
            TriggerState.Normal,
            DateTimeOffset.UtcNow.AddMinutes(1));
        var pagedResult = new PagedResult<TriggerHeader>([header], false);
        this.mockScheduler.Setup(s => s.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var service = this.CreateHealthService(awsOptions: awsOptions);

        // Act
        var healthCheckResponse = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Healthy), healthCheckResponse.Status);
        Assert.Equal(3, healthCheckResponse.Entries.Count);
        Assert.Equal(nameof(HealthStatus.Healthy), healthCheckResponse.Entries["database"].Status);
        Assert.Equal(nameof(HealthStatus.Healthy), healthCheckResponse.Entries["queue"].Status);
        Assert.Equal(nameof(HealthStatus.Healthy), healthCheckResponse.Entries["quartz"].Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenComponentIsUnhealthy_ReturnsUnhealthy()
    {
        // Arrange
        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });

        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSQSException("Queue is down"));

        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.Status).Returns(SchedulerStatus.Running);

        var service = this.CreateHealthService(awsOptions: awsOptions);

        // Act
        var healthCheckResponse = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), healthCheckResponse.Status);
        Assert.Equal(nameof(HealthStatus.Healthy), healthCheckResponse.Entries["database"].Status);
        Assert.Equal(nameof(HealthStatus.Unhealthy), healthCheckResponse.Entries["queue"].Status);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenSqsTimesOut_ReturnsUnhealthy()
    {
        // Arrange
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

        // Act
        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("timed out", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenSchedulerIsShutdown_ReturnsUnhealthy()
    {
        // Arrange
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.Status).Returns(SchedulerStatus.Shutdown);
        this.mockScheduler.Setup(s => s.SchedulerName).Returns("TestScheduler");

        var service = this.CreateHealthService();

        // Act
        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("shutdown", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenJobEnabledAndRegisteredWithTrigger_ReturnsHealthy()
    {
        // Arrange
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.Status).Returns(SchedulerStatus.Running);
        this.mockScheduler.Setup(s =>
                s.Exists(It.Is<JobKey>(k => k.Name == nameof(CtsBundlePollingJob)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var nextFire = DateTimeOffset.UtcNow.AddMinutes(5);
        var header = CreateTriggerHeader(new TriggerKey("test-trigger"), TriggerState.Normal, nextFire);
        this.mockScheduler.Setup(s => s.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TriggerHeader>([header], false));

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        // Act
        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Contains("scheduled", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Data);
        Assert.Equal(nextFire.ToString("o"), result.Data["nextFireTimeUtc"]);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenJobEnabledButNotRegistered_ReturnsUnhealthy()
    {
        // Arrange
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.Status).Returns(SchedulerStatus.Running);
        this.mockScheduler.Setup(s => s.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        this.mockScheduler.Setup(s => s.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TriggerHeader>([], false));

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        // Act
        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("not registered", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenTriggerInErrorState_ReturnsUnhealthy()
    {
        // Arrange
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.Status).Returns(SchedulerStatus.Running);
        this.mockScheduler.Setup(s => s.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var header = CreateTriggerHeader(new TriggerKey("error-trigger"), TriggerState.Error);
        this.mockScheduler.Setup(s => s.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TriggerHeader>([header], false));

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        // Act
        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("Error state", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenSchedulerTimesOut_ReturnsUnhealthy()
    {
        // Arrange
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                return this.mockScheduler.Object;
            });

        var service = this.CreateHealthService(
            checkTimeout: TimeSpan.FromMilliseconds(50));

        // Act
        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("timed out", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenSchedulerFactoryReturnsNull_ReturnsUnhealthy()
    {
        // Arrange
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IScheduler)null!);

        var service = this.CreateHealthService();

        // Act
        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Equal("Quartz scheduler instance could not be obtained.", result.Description);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenJobDisabled_ReturnsHealthy()
    {
        // Arrange
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.Status).Returns(SchedulerStatus.Running);
        this.mockScheduler.Setup(s => s.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        this.mockScheduler.Setup(s => s.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TriggerHeader>([], false));

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = false }));

        // Act
        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.Contains("disabled by configuration", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenJobHasNoTriggers_ReturnsDegraded()
    {
        // Arrange
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.Status).Returns(SchedulerStatus.Running);
        this.mockScheduler.Setup(s => s.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockScheduler.Setup(s => s.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TriggerHeader>([], false));

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        // Act
        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Degraded), result.Status);
        Assert.Contains("has no triggers scheduled", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenTriggerInPausedState_ReturnsDegraded()
    {
        // Arrange
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.Status).Returns(SchedulerStatus.Running);
        this.mockScheduler.Setup(s => s.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var header = CreateTriggerHeader(new TriggerKey("paused-trigger"), TriggerState.Paused);
        this.mockScheduler.Setup(s => s.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TriggerHeader>([header], false));

        var service = this.CreateHealthService(
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        // Act
        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Degraded), result.Status);
        Assert.Contains("Paused", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuartzHealthAsync_WhenSchedulerThrowsException_ReturnsUnhealthy()
    {
        // Arrange
        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Scheduler crash"));

        var service = this.CreateHealthService();

        // Act
        var result = await service.CheckQuartzHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Unhealthy), result.Status);
        Assert.Contains("Scheduler crash", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenComponentIsDegraded_ReturnsDegradedOverall()
    {
        // Arrange
        var queueUrl = "https://sqs.eu-west-2.amazonaws.com/123/submission-validation-queue";
        var awsOptions = Options.Create(new AwsMessagingOptions { SubmissionValidationQueueUrl = queueUrl });
        this.mockSqs.Setup(s =>
                s.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse());

        this.mockSchedulerFactory.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(this.mockScheduler.Object);
        this.mockScheduler.Setup(s => s.Status).Returns(SchedulerStatus.Running);
        this.mockScheduler.Setup(s => s.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockScheduler.Setup(s => s.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TriggerHeader>([], false));

        var service = this.CreateHealthService(
            awsOptions: awsOptions,
            ctsJobOptions: Options.Create(new CtsPollingJobOptions { Enabled = true }));

        // Act
        var response = await service.CheckHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Degraded), response.Status);
        Assert.Equal(nameof(HealthStatus.Healthy), response.Entries["database"].Status);
        Assert.Equal(nameof(HealthStatus.Healthy), response.Entries["queue"].Status);
        Assert.Equal(nameof(HealthStatus.Degraded), response.Entries["quartz"].Status);
    }

    [Fact]
    public async Task CheckQueueHealthAsync_WhenAttributesDoNotContainMessageCount_DefaultsToZero()
    {
        // Arrange
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

        // Act
        var result = await service.CheckQueueHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(nameof(HealthStatus.Healthy), result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data["approximateNumberOfMessages"]);
    }

    public void Dispose()
    {
        this.defaultDbContext.Dispose();
    }

    private static TriggerHeader CreateTriggerHeader(
        TriggerKey key,
        TriggerState state,
        DateTimeOffset? nextFireTimeUtc = null)
    {
        return new TriggerHeader(
            key,
            new JobKey("test"),
            "test-type",
            "test-desc",
            state,
            DateTimeOffset.UtcNow,
            null,
            nextFireTimeUtc,
            null,
            null,
            0,
            null,
            null,
            0);
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
