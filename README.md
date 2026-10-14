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
### Service-to-service authentication

Every versioned endpoint (`/v1/...`) requires an `x-api-key` header that matches one of the configured keys,
otherwise it returns `401` problem details. `/health`, the OpenAPI document and Scalar stay anonymous. This is the
interim mechanism agreed in [LREG-560](https://eaflood.atlassian.net/browse/LREG-560) until AWS STS replaces it; the
endpoints only reference the `ServiceToService` authorisation policy, so STS is added as another scheme on that policy.

| Setting | Purpose |
| --- | --- |
| `ApiKeyAuthentication__Keys__0` | Accepted key. Set as a CDP secret; it must equal `CattleApi__ApiKey` in `lis-be4fe-cattle-home`. |
| `ApiKeyAuthentication__Keys__1` | Optional second key, so the key can be rotated without downtime. |

With no key configured the service still starts and answers `/health`, but rejects every API request. Locally,
`development-compose.yml`, `compose.override.yml` and the launch profiles use `local-dev-cattle-api-key`, and the
`.http` files send it from `tests/Endpoints/http-client.env.json`.

### Correlation ID

Every versioned endpoint also requires a single `x-cdp-request-id` header (the
[Correlation ID standard](https://eaflood.atlassian.net/wiki/spaces/LDD/pages/6438093586/Correlation+ID+standard));
without it the request is rejected with `400` problem details carrying `"code": "missing_header"`. CDP and the
BE4FE always send it; `/health`, the OpenAPI document and Scalar do not need it. The value is:

- echoed on the response and included as `correlationId` in error problem details;
- written as `CorrelationId` on every log line (ECS JSON in CDP, plain text locally);
- forwarded on every upstream call (KRDS, CADS, CTS).

The `.http` files send a fresh `{{$guid}}`; send your own when calling the API by hand, for example
`-H 'x-cdp-request-id: local-test-1'`.

### Running locally against CDP dev

To test the API from a developer machine against the operational KRDS and CADS services in CDP dev, call them through
the `ephemeral-protected` gateway. Every gateway call needs a
[CDP developer API key](https://eaflood.atlassian.net/wiki/spaces/LDD/pages/6449987795/CDP+developer+API+key)
(from the CDP portal) as `x-api-key`, as well as the usual KRDS or CADS Basic credentials. Set `GatewayApiKey` and the
service sends the header on every upstream call. Leave it unset in CDP, where services call each other directly, and
against lis-fake-service.

Export the settings in your shell, or keep them in an untracked file you `source`. Never commit them. Run without a
launch profile: the `http` profile sets the fake-service URLs and credentials, and they override exported variables.
Postgres and LocalStack must be running, as for any local run.

```bash
export ASPNETCORE_ENVIRONMENT=Development
export ApiKeyAuthentication__Keys__0=local-dev-cattle-api-key
export KrdsApi__BaseUrl=https://ephemeral-protected.api.dev.cdp-int.defra.cloud/ls-keeper-data-api/
export KrdsApi__ClientId=<KRDS ACL client id>
export KrdsApi__ClientSecret=<KRDS ACL client secret>
export KrdsApi__GatewayApiKey=<CDP developer API key>
export CadsApi__BaseUrl=https://ephemeral-protected.api.dev.cdp-int.defra.cloud/cads-data-service/
export CadsApi__ClientId=<CADS client id>
export CadsApi__ClientSecret=<CADS client secret>
export CadsApi__GatewayApiKey=<CDP developer API key>
dotnet run --project src/Api --no-launch-profile -- --urls http://localhost:5019
```

Each base URL keeps its trailing slash, because the services append API-relative paths. Port 5019 is the one
the BE4FE's development settings call (`CattleApi__BaseUrl`). Then use the `.http` files with
the `dev-operational` environment in `tests/Endpoints/http-client.env.json`:

- `Upstreams/OperationalDev.http` calls KRDS and CADS directly, to check the credentials and test data before
  involving the cattle API.
- `Cattle`, `Users` and `Registrations` call the local cattle API, which calls dev.
- `Security/Security.http` checks that a missing or wrong `x-api-key` gives `401` and a missing `x-cdp-request-id` gives
  `400 missing_header`.

Put `cdpDeveloperApiKey`, `krdsClientId`, `krdsClientSecret`, `cadsClientId` and `cadsClientSecret` in
`tests/Endpoints/http-client.private.env.json` under `dev-operational`. The file is gitignored. Replace the
placeholder `cph`, `earTag` and `subject` with identifiers that exist in dev. Do not commit responses: they contain
real keeper data.
