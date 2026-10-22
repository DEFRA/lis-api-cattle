# Messaging

The API validates cattle registration submissions through an AWS SQS queue, and can also publish an
event to an SNS topic. See [Messaging](../README.md#messaging) in the README for how the code uses them.

## Naming rules

Queue and topic names must match `/^[a-z0-9][a-z0-9_]+[a-z0-9]$/`. Use lowercase letters, digits and
underscores (`snake_case`). Hyphens, capitals, full stops and spaces are not allowed, so
`submission-validation-queue` is rejected and `submission_validation_queue` is accepted.

## Local names

The repo's compose files and `src/Api/appsettings.Development.json` use:

| Resource | Name |
|---|---|
| Validation queue | `submission_validation_queue` |
| Event topic | `submission_validation_topic` |

```json
{
  "AWS": {
    "Region": "eu-west-2",
    "ServiceUrl": "http://localhost:4566",
    "UseLocalStack": true,
    "SubmissionValidationQueueUrl": "http://localhost:4566/000000000000/submission_validation_queue",
    "SubmissionValidationTopicArn": "arn:aws:sns:eu-west-2:000000000000:submission_validation_topic"
  }
}
```

LocalStack creates the resources from `compose/start-localstack.sh`.

## Names in deployed environments

Every deployed environment (dev, test, ext-test, perf-test and production) uses the queue
`lis_cattle_submission_intake`, set in `cdp/app-config` as `AWS__SubmissionValidationQueueUrl`. Check
the CDP portal for the topic and for any dead-letter queue, because this repo does not create them.
