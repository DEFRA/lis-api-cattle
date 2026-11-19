// <copyright file="LocalStackFixture.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Tests.Integration;

using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

/// <summary>
/// LocalStack SQS in a container, the same image as the repo's compose files.
/// </summary>
public sealed class LocalStackFixture : IAsyncLifetime
{
    private const int Port = 4566;

    private readonly IContainer container = new ContainerBuilder("localstack/localstack:4.10.0")
        .WithEnvironment("SERVICES", "sqs")
        .WithPortBinding(Port, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(Port).ForPath("/_localstack/health")))
        .Build();

    public string ServiceUrl => $"http://{container.Hostname}:{container.GetMappedPublicPort(Port)}";

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public ValueTask DisposeAsync() => container.DisposeAsync();

    public IAmazonSQS CreateClient() => new AmazonSQSClient(
        new BasicAWSCredentials("test", "test"),
        new AmazonSQSConfig { ServiceURL = ServiceUrl, AuthenticationRegion = "eu-west-2" });

    /// <summary>
    /// Creates a queue with its own dead-letter queue, as compose/start-localstack.sh does.
    /// </summary>
    /// <param name="visibilityTimeoutSeconds">The queue's visibility timeout.</param>
    /// <param name="maxReceiveCount">The receives after which a message moves to the dead-letter queue.</param>
    /// <returns>The queue URL and the dead-letter queue URL.</returns>
    public async Task<(string QueueUrl, string DeadLetterQueueUrl)> CreateQueueAsync(int visibilityTimeoutSeconds, int maxReceiveCount)
    {
        using var sqs = CreateClient();
        var name = $"lis_cattle_registration_bundle_{Guid.NewGuid():N}";
        var dlq = await sqs.CreateQueueAsync(name + "_dlq");
        var dlqArn = (await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest(dlq.QueueUrl, ["QueueArn"]))).QueueARN;
        var queue = await sqs.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = name,
            Attributes = new Dictionary<string, string>
            {
                ["VisibilityTimeout"] = visibilityTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["RedrivePolicy"] = $$"""{"deadLetterTargetArn":"{{dlqArn}}","maxReceiveCount":"{{maxReceiveCount}}"}""",
            },
        });
        return (queue.QueueUrl, dlq.QueueUrl);
    }

    public async Task<int> CountMessagesAsync(string queueUrl)
    {
        using var sqs = CreateClient();
        var attributes = await sqs.GetQueueAttributesAsync(new GetQueueAttributesRequest(
            queueUrl,
            ["ApproximateNumberOfMessages", "ApproximateNumberOfMessagesNotVisible"]));
        return attributes.ApproximateNumberOfMessages + attributes.ApproximateNumberOfMessagesNotVisible;
    }
}
