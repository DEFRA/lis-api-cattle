### Cause of Failure

The pattern `/^[a-z0-9][a-z0-9_]+[a-z0-9]$/` enforces:
1. **Allowed characters**: lowercase alphanumeric characters (`a-z`, `0-9`) and **underscores** (`_`).
2. **Disallowed characters**: **Hyphens (`-`)**, uppercase characters, periods, and spaces.
3. **Length & boundary rules**: Must start and end with an alphanumeric character (`[a-z0-9]`), with at least one character in between.

The name `submission-validation-queue` fails because it uses hyphens (`-`) instead of underscores (`_`).

---

### Suitable Queue Names Matching `/^[a-z0-9][a-z0-9_]+[a-z0-9]$/`

To comply with the regex rule, use `snake_case` naming across your queues and topics:

#### 1. Core Submission Validation Queues
- **Local / Default Queue**: `submission_validation_queue`
- **Dead-Letter Queue (DLQ)**: `submission_validation_queue_dlq`

#### 2. Environment-Specific SQS Queue Names
- **Dev**: `lis_cattle_submission_validation_dev` (DLQ: `lis_cattle_submission_validation_dev_dlq`)
- **Test**: `lis_cattle_submission_validation_test` (DLQ: `lis_cattle_submission_validation_test_dlq`)
- **Pre-Prod**: `lis_cattle_submission_validation_preprod` (DLQ: `lis_cattle_submission_validation_preprod_dlq`)
- **Production**: `lis_cattle_submission_validation_prod` (DLQ: `lis_cattle_submission_validation_prod_dlq`)

#### 3. Intake Queues (for Third-Party / External Ingestion)
- **Queue**: `lis_cattle_submission_intake_prod`
- **DLQ**: `lis_cattle_submission_intake_prod_dlq`

#### 4. SNS Event Topics
- **LocalStack**: `submission_validation_topic`
- **Cloud Environments**: `lis_cattle_submission_validation_events_prod`

---

### Updated Project Defaults

The repository configuration and LocalStack bootstrapping scripts have been updated to use valid `snake_case` names:

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

These names conform directly to `/^[a-z0-9][a-z0-9_]+[a-z0-9]$/` and align with existing resource conventions in `compose/start-localstack.sh` (such as `identity_service_helper_intake` and `ls_keeper_data_import_complete`).