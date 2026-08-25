namespace Lis.Cattle.Configurations;

public class AwsMessagingOptions
{
    public const string SectionName = "AWS";

    public string Region { get; set; } = "eu-west-2";

    public string? ServiceUrl { get; set; } = "http://localhost:4566";

    public bool UseLocalStack { get; set; } = true;

    public string SubmissionValidationQueueUrl { get; set; } = "http://localhost:4566/000000000000/submission-validation-queue";

    public string? SubmissionValidationTopicArn { get; set; } = "arn:aws:sns:eu-west-2:000000000000:submission-validation-topic";

    public bool EnableBackgroundConsumer { get; set; } = true;

    public int PollingIntervalSeconds { get; set; } = 10;

    public int MaxNumberOfMessages { get; set; } = 10;

    public int WaitTimeSeconds { get; set; } = 5;
}
