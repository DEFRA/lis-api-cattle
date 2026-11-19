// <copyright file="AwsMessagingOptions.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Api.Configurations;

public class AwsMessagingOptions
{
    public const string SectionName = "AWS";

    public string Region { get; set; } = "eu-west-2";

    public string? ServiceUrl { get; set; }

    public bool UseLocalStack { get; set; }

    public string SubmissionValidationQueueUrl { get; set; } = string.Empty;

    public string? SubmissionValidationTopicArn { get; set; }

    public bool EnableBackgroundConsumer { get; set; } = true;

    public int PollingIntervalSeconds { get; set; } = 10;

    public int MaxNumberOfMessages { get; set; } = 10;

    public int WaitTimeSeconds { get; set; } = 5;

    // The queue the registration bundle worker reads (LREG-925). It must not be the validation queue: the two
    // consumers expect different messages.
    public string RegistrationBundleQueueUrl { get; set; } = string.Empty;

    public bool EnableRegistrationBundleConsumer { get; set; } = true;

    // How long a received bundle stays hidden from other instances. It must be longer than the worst-case
    // processing time (Postgres plus every CTS attempt), or a second instance could pick the bundle up.
    public int RegistrationBundleVisibilityTimeoutSeconds { get; set; } = 300;

    // The queue's redrive maxReceiveCount. The worker logs an error with the correlation ID on the last attempt
    // before SQS moves the message to the dead-letter queue.
    public int RegistrationBundleMaxReceiveCount { get; set; } = 5;
}
