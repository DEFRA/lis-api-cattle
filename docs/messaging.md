# Messaging

The API queues registration bundles for CTS through one AWS SQS queue (with a dead-letter queue), and validates
cattle registration submissions through another, which can also publish an event to an SNS topic. See [Messaging](../README.md#messaging) in the README for how the code uses them.

## Naming rules

Queue and topic names must match `/^[a-z0-9][a-z0-9_]+[a-z0-9]$/`. Use lowercase letters, digits and
underscores (`snake_case`). Hyphens, capitals, full stops and spaces are not allowed, so
`submission-validation-queue` is rejected and `submission_validation_queue` is accepted.

## Local names

The repo's compose files and `src/Api/appsettings.Development.json` use:

| Resource | Name |
|---|---|
| Validation queue | `submission_validation_queue` |
| Registration bundle queue | `lis_cattle_registration_bundle` (visibility timeout 300 s, redrive to the DLQ after 5 receives) |
| Registration bundle dead-letter queue | `lis_cattle_registration_bundle_dlq` |
| Event topic | `submission_validation_topic` |

```json
{
  "AWS": {
    "Region": "eu-west-2",
    "ServiceUrl": "http://localhost:4566",
    "UseLocalStack": true,
    "SubmissionValidationQueueUrl": "http://localhost:4566/000000000000/submission_validation_queue",
    "SubmissionValidationTopicArn": "arn:aws:sns:eu-west-2:000000000000:submission_validation_topic",
    "RegistrationBundleQueueUrl": "http://localhost:4566/000000000000/lis_cattle_registration_bundle"
  }
}
```

LocalStack creates the resources from `compose/start-localstack.sh` (and `compose/start-localstack-override.sh`
for `compose.override.yml`).

## Names in deployed environments

Every deployed environment (dev, test, ext-test, perf-test and production) uses the queue
`lis_cattle_submission_intake`, set in `cdp/app-config` as `AWS__SubmissionValidationQueueUrl`. Check
the CDP portal for the topic and for any dead-letter queue, because this repo does not create them.

The registration bundle queue and its dead-letter queue must be created in CDP for each environment before LREG-925
is deployed, and `AWS__RegistrationBundleQueueUrl` set in `cdp/app-config`. It must be a different queue from
`lis_cattle_submission_intake`: the validation consumer reads that queue and expects a different message. Set the
queue's visibility timeout to at least `AWS__RegistrationBundleVisibilityTimeoutSeconds` (300 s), and its redrive
`maxReceiveCount` to `AWS__RegistrationBundleMaxReceiveCount` (5).
