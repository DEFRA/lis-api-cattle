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
}
