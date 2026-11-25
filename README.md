# lis-api-cattle

Part of the Livestock Information Service (LIS), which lets livestock keepers, owners, agents and
delegates record and manage their animals.

The cattle API is the backend for cattle in LIS. It is a .NET 10 minimal API that returns holdings
and the cattle on them, creates and validates cattle registration submissions, and keeps the
accounts of signed-in users. It stores submissions in Postgres, reads holding and animal data from
KRDS and CADS, and sends submitted bundles to CTS (the Cattle Tracing System) through an SQS queue.

Deployed on the Core Delivery Platform (CDP). See the
[CDP portal service page](https://portal.cdp-int.defra.cloud/services/lis-api-cattle) for
environments, deployments, logs and metrics.

## Architecture

```text
hub → /cattle → lis-apps-cattle-home → lis-be4fe-cattle-home → this API → KRDS, CADS, CTS
```

- Callers: [lis-be4fe-cattle-home](https://github.com/DEFRA/lis-be4fe-cattle-home) is the only
  caller in the lis-dev stack. It sends the holdings, cattle and user requests on behalf of the
  cattle microsites.
- Authentication: every `/v1` endpoint needs an `x-api-key` header, and `x-cdp-request-id` (see
  [Service-to-service authentication](#service-to-service-authentication) and
  [Correlation ID](#correlation-id)). `/health`, the public OpenAPI document and Scalar are anonymous; the internal
  OpenAPI document needs `x-api-key`.
- Downstream calls:
  - KRDS (keeper and holding data) and CADS (animals on a holding) over HTTP, each with Basic
    credentials (`ClientId` and `ClientSecret`).
  - CTS Web Services (`CtsWeb__Url`): the `TransferDataHex` SOAP operation, for Birth requests. Locally this is
    the lis-fake-service `/cts_ws` fake. See [Messaging](#messaging).
  - AWS SQS (registration bundles and submission validation), SNS (validation events) and S3 (the temporary CADS
    data patch).
  - Postgres, through Entity Framework Core.

### Endpoints

Every `/v1` endpoint requires the `x-api-key` and `x-cdp-request-id` headers. Two OpenAPI documents are
published per API version, describing the same routes:

- `/openapi/v1.json` (public, anonymous) lists only the endpoints marked `.WithPublicApi()`. Scalar shows this
  document in Development only.
- `/openapi/v1-internal.json` (internal, needs `x-api-key`) lists every endpoint, including `/health/detailed`. It
  is also generated at build time.

`dotnet build Cattle.slnx` generates both OpenAPI 3.1 documents and copies them to
`artifacts/openapi/v1.json` and `artifacts/openapi/v1-internal.json`, ready for artifact collection.
The Cake build and `dotnet publish` run the same generation step. These files are gitignored.

Endpoints are internal-only until marked. `OpenApiConfigurationTests` lists the operations agreed as public, so a new
public endpoint has to be added there too.

| Method and path | Purpose |
|---|---|
| `GET /v1/holdings` | Search holdings |
| `GET /v1/holdings/{county}/{parish}/{holding}` | Get the holding details |
| `GET /v1/holdings/{county}/{parish}/{holding}/cattle` | Cattle on the holding |
| `GET /v1/holdings/{cph}/cattle` | Cattle on the holding, by CPH |
| `GET /v1/holdings/{county}/{parish}/{holding}/bundles` | Registration bundles for the holding |
| `GET /v1/holdings/{cph}/bundles` | Registration bundles for the holding, by CPH |
| `GET /v1/cattle/{earTag}` | Get the cattle details |
| `POST /v1/registrations` | Submit a cattle registration bundle (queued; returns `200` with the bundle ID) |
| `POST /v1/registrations/{id}/validate` | Validate a cattle registration bundle |
| `GET /v1/users/{subject}` | Get a user's details and associated CPHs |
| `POST /v1/users` | Create or refresh a user account |
| `GET /health` | Health check (anonymous) |
| `GET /health/detailed` | Detailed health status (anonymous) |

### Service-to-service authentication

Every versioned endpoint (`/v1/...`) requires an `x-api-key` header that matches one of the configured keys,
otherwise it returns `401` problem details. So does the internal OpenAPI document (`/openapi/v1-internal.json`).
`/health`, the public OpenAPI document and Scalar stay anonymous. This is the
interim mechanism agreed in [LREG-560](https://eaflood.atlassian.net/browse/LREG-560) until AWS STS replaces it; the
endpoints only reference the `ServiceToService` authorisation policy, so STS is added as another scheme on that policy.

| Setting | Purpose |
|---|---|
| `ApiKeyAuthentication__Keys__0` | Accepted key. Set as a CDP secret; it must equal `CattleApi__ApiKey` in `lis-be4fe-cattle-home`. |
| `ApiKeyAuthentication__Keys__1` | Optional second key, so the key can be rotated without downtime. |

With no key configured the service still starts and answers `/health`, but rejects every API request. Locally,
`development-compose.yml`, `compose.override.yml` and the launch profiles use `local-dev-cattle-api-key`, and the
`.http` files send it from `tests/Endpoints/http-client.env.json`.

### Correlation ID

Every versioned endpoint also requires a single `x-cdp-request-id` header (the
[Correlation ID standard](https://eaflood.atlassian.net/wiki/spaces/LDD/pages/6438093586/Correlation+ID+standard));
without it the request is rejected with `400` problem details carrying `"code": "missing_header"`. CDP and the
BE4FE always send it; `/health`, the OpenAPI documents and Scalar do not need it. The value is:

- echoed on the response and included as `correlationId` in error problem details;
- written as `CorrelationId` on every log line (ECS JSON in CDP, plain text locally);
- forwarded on every upstream call (KRDS, CADS, CTS).

The `.http` files send a fresh `{{$guid}}`; send your own when calling the API by hand, for example
`-H 'x-cdp-request-id: local-test-1'`.

### Upstream resilience and circuit breakers (CADS and KRDS)

Calls to upstream REST services (KRDS and CADS) are managed through the Defra Livestock SDK strategy framework (`Defra.Livestock.Sdk.Api.Strategies`). Each service (`KrdsService` and `CadsService`) uses:

- **REST strategy factory** (`IRestStrategyFactory<TService>`): handles base URLs, credentials (Basic Auth and optional `x-api-key` developer gateway key), JSON serialization, and HTTP operations.
- **Circuit breaker strategy factory** (`ICircuitBreakerStrategyFactory<TService>`): wraps upstream operations with specific circuit keys:
  - `KrdsService`:
    - `GetHoldingAsync` &rarr; `"get-holding-from-krds"`
    - `SearchHoldingsAsync` &rarr; `"search-holdings-from-krds"`
    - `GetUserAccountAsync` &rarr; `"get-user-account-from-krds"`
    - `EnsureUserAccountAsync` &rarr; `"ensure-user-account-in-krds"`
  - `CadsService`:
    - `GetAnimalDetailsAsync` &rarr; `"get-animal-details-from-cads"`
    - `GetAnimalsPagedAsync` &rarr; `"get-animals-on-holding-paged-from-cads"`

The circuit breaker strategy uses the HTTP policy created by `CircuitBreakerPolicyFactory.CreateHttpPolicy()` (`ICircuitBreakerPolicyFactory`) to determine which upstream HTTP errors and exceptions are captured by the circuit breakers, breaking the circuit to prevent cascading failures when upstream services are degraded or failing.

Configurable circuit breaker settings and their defaults:

| Setting | Description | Default |
|---|---|---|
| `CircuitBreakerConsecutiveFailureThreshold` | Number of consecutive captured failures before the circuit opens | `5` (`appsettings.json`) |
| `CircuitBreakerFailureRatio` | Failure ratio (0.0 – 1.0) threshold during the sampling window | `0.5` (50%) |
| `CircuitBreakerSamplingDuration` | Time window in milliseconds over which failure metrics are evaluated | `30000` (30s) |
| `CircuitBreakerDurationOfBreak` | Duration in milliseconds that the circuit stays open before transitioning to half-open | `30000` (30s) |
| `CircuitBreakerMinimumThroughput` | Minimum number of calls within the sampling window required before ratio thresholds apply | `10` |

When a circuit breaker is open, calls fail fast by throwing a `CircuitBreakerOpenException` (matching `exception.IsCircuitBreakerOpen()`), which `ApiExceptionHandler` catches and converts into a `503 Service Unavailable` response.

### Global exception handling

Global exceptions are processed centrally by `ApiExceptionHandler`, an implementation of ASP.NET Core's `IExceptionHandler`:

- **RFC 7807 Problem Details**: Unhandled exceptions are converted into standard `ProblemDetails` JSON responses with appropriate HTTP status codes:
  - `400 Bad Request`: `BusinessRuleException`, `ReferenceRuleException`, `RequestValidationException`, `ArgumentException`.
  - `403 Forbidden`: `UnauthorizedAccessException`.
  - `404 Not Found`: `NotFoundException`, `EntityNotFoundException`, `ExistenceRuleException`.
  - `409 Conflict`: `ConflictException`, `ConflictRuleException`.
  - `503 Service Unavailable`: `ServiceUnavailableException`, and any exception where the circuit breaker is open (`exception.IsCircuitBreakerOpen()`, such as `CircuitBreakerOpenException`).
  - `500 Internal Server Error`: All other unhandled exceptions.
- **Correlation and Traceability**: Re-applies the `x-cdp-request-id` response header and includes `correlationId` and `traceId` extension properties in the problem response body.
- **Structured Logging**: Enriches the Serilog `LogContext` with `CorrelationId`, `TraceId`, `Path` (using route templates rather than raw paths to avoid logging sensitive data), and `StatusCode`. Client errors (`< 500`) are logged as warnings, while server errors (`>= 500`, including `503` when the circuit breaker is open) are logged as errors with full exception details.

### Data

The API uses Postgres. In CDP it connects to Aurora with IAM authentication, using separate
read-write and read-only hosts. Locally it uses the connection strings
`ConnectionStrings__ReadWritePostgresConnection` and `ConnectionStrings__ReadOnlyPostgresConnection`.

The schema is managed with Liquibase. The changelog is in [changelog/](changelog), with
`db.changelog.xml` as the entry point and the SQL under `changelog/schema/`. The API does not run
migrations itself.

- Locally with lis-dev: the `lis-api-cattle-liquibase` service downloads the changelog for the API
  version in use (`LIS_API_CATTLE_VERSION`) and applies it before the API starts. See the
  [liquibase image README](https://github.com/DEFRA/lis-dev/blob/main/.tools/liquibase/README.md),
  which also explains how to run a changelog from disk while you write a migration.
- Locally with this repo's compose files: the `liquibase` service applies `./changelog`.
- In CDP: the publish pipeline uploads the changelog to S3 and for CDP to apply. See
  `liquibase.cake` and `.github/workflows/publish.yml`.
- In the tests: the integration tests apply `changelog/schema` to a Postgres container.

### Messaging

Registration bundles are submitted asynchronously (LREG-925).

- `POST /v1/registrations` checks the shape of the bundle (`RegistrationBundleRequestValidator`), gives it a
  submission ID, and sends it to the SQS queue in `AWS__RegistrationBundleQueueUrl` with the correlation ID as the
  `CorrelationId` message attribute. It returns `200` with the ID straight away, `400` problem details for an invalid
  shape (nothing is queued), or `503` when SQS cannot take the message.
- `RegistrationBundleBackgroundService` reads the queue. For each message `RegistrationBundleHandler`:
  1. stores the bundle and its animals (status `submitted`) and the CTSWeb `Register_Births_Asynchronous` request,
     keyed by the submission ID, so a redelivered message is not stored twice. The request is stored without its
     `Authentication` element, so the CTS Online password never reaches the database;
  2. locks the submission row and, if the bundle has no CTS outcome yet, sends the stored request through
     `CtsWebClient` (one `Birth` row per animal, `TxnId` = the submission ID);
  3. stores the CTS receipt number and sent time, or marks the bundle `error` with the CTS error code and text when
     CTS rejects it or cannot be reached after `CtsWeb__MaxAttempts` attempts. CTS failures are not retried.
- The message is deleted only after the outcome is saved. A database failure or an unreadable message leaves it on
  the queue, so SQS delivers it again after the visibility timeout and moves it to the dead-letter queue after the
  queue's `maxReceiveCount`. The last attempt is logged as an error with the correlation ID.
- Several instances can run: SQS hides a received message from the others, and the row lock stops two deliveries
  of the same bundle both sending it.

Submissions can still be validated with `POST /v1/registrations/{id}/validate`:

- `SubmissionValidationBackgroundService` polls the queue in `AWS__SubmissionValidationQueueUrl` and validates
  each submission it is sent. Since LREG-925 nothing in this service sends to that queue: bundles are no longer
  queued for validation when they are created. Set `AWS__EnableBackgroundConsumer` to `false` to turn both
  consumers off.

Queue and topic naming rules, and the local and planned names, are in [docs/messaging.md](docs/messaging.md).

CTS validates a bundle after it accepts it, so `CtsBundlePollingJob` (Quartz) fetches the results (LREG-926):

- Each run claims up to `CtsPollingJob__BatchSize` bundles that are `submitted` with a receipt and no
  `completed_at`, with `FOR UPDATE SKIP LOCKED` (bundles polled longest ago first), so two instances never poll the
  same bundle in one cycle.
- It makes one `Get_Register_Births_Validation_Results` call per bundle with the receipt. Each accepted row marks
  its animal `complete`; each rejected row marks its animal `error` and stores one `submission_animal_errors` row
  per CTS cause. Rows are matched to animals by `RowNum` through the stored request. Animals CTS has not returned
  yet, and animals that already have an outcome, are left as they are, so polling the same results twice changes
  nothing.
- When every animal has an outcome the bundle is completed: `complete` if CTS accepted them all, otherwise
  `complete_with_errors`, with `completed_at` and `updated_at` set. A completed bundle is not polled again.
- "Results not ready" (`CTWS806`), CTS being unavailable and other CTS errors leave the bundle for the next run and
  are logged with the bundle ID and correlation ID. A receipt CTS does not recognise (`CTWS002`) is retried until
  `CtsPollingJob__UnknownReceiptMaxAgeHours` after the bundle was sent, then the bundle is marked `error`.

### Temporary CADS data patch

Until CADS returns the full animal details, the API reads patch files from the S3 bucket in
`CadsDataPatch__BucketName`: a folder per CPH (`{cph}/`) holding one `{earTag}.json` file per animal.

## Prerequisites

- .NET SDK 10 (`global.json` sets 10.0.0 and allows later feature bands)
- Docker with Docker Compose (for lis-dev, the repo's compose files and the integration tests)
- A local clone of [lis-dev](https://github.com/DEFRA/lis-dev) (recommended, see [Running with lis-dev](#running-with-lis-dev-recommended))

## Setup

### Development

```bash
dotnet restore Cattle.slnx
dotnet build Cattle.slnx
```

Configuration comes from `src/Api/appsettings.json`, then `appsettings.Development.json` when
`ASPNETCORE_ENVIRONMENT` is `Development`, then environment variables. In a variable name, use
`__` for the `:` between configuration levels (`KrdsApi__BaseUrl` sets `KrdsApi:BaseUrl`).

The `http` launch profile in `src/Api/Properties/launchSettings.json` sets the local values (port
8091, LocalStack, fake KRDS and CADS, and the local API key).

### Test

The test project needs no extra tools. Tests use xUnit. The integration tests need Docker.

## Running with lis-dev (recommended)

The API only works properly with its Postgres, the Liquibase migration, LocalStack, the fake KRDS
and CADS service (`lis-fake-service`), and its caller `lis-be4fe-cattle-home`.
[lis-dev](https://github.com/DEFRA/lis-dev) runs all of these locally with Docker Compose, so it is
the recommended way to run and work on this repo. Follow its README for the one-time setup.

Start the stack:

```bash
cd lis-dev
docker compose up -d
```

In compose this API is the `lis-api-cattle` service, published on host port 8091 (container port
8085). It starts after Postgres, LocalStack and `lis-api-cattle-liquibase`, which applies this repo's
changelog to the stack's Postgres (database `lis_cattle`, published on host port 5433). Set
`LIS_API_CATTLE_VERSION` to run another image tag and matching migrations.

`.bin/sideload` supports Node.js services only, so this API cannot be sideloaded. To work on it,
stop the container and run the API on your machine against the rest of the stack:

```bash
docker compose stop lis-api-cattle
cd api/cattle
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__ReadWritePostgresConnection="User ID=lis_api_cattle_ddl;Password=postgres;Host=localhost;Port=5433;Database=lis_cattle;"
export ConnectionStrings__ReadOnlyPostgresConnection="$ConnectionStrings__ReadWritePostgresConnection"
export ApiKeyAuthentication__Keys__0=local-dev-cattle-api-key
export CadsApi__BaseUrl=http://localhost:3010/cads/
export KrdsApi__BaseUrl=http://localhost:3010/krds/
export AWS_REGION=eu-west-2
export AWS_ACCESS_KEY_ID=lis-api-cattle
export AWS_SECRET_ACCESS_KEY=test
export AWS__UseLocalStack=true
export AWS__ServiceUrl=http://localhost:4566
export AWS__SubmissionValidationQueueUrl=http://localhost:4566/000000000000/submission_validation_queue
export AWS__SubmissionValidationTopicArn=arn:aws:sns:eu-west-2:000000000000:submission_validation_topic
export AWS__RegistrationBundleQueueUrl=http://localhost:4566/000000000000/lis_cattle_registration_bundle
dotnet run --project src/Api --no-launch-profile --urls http://localhost:8091
```

The values are the ones in `lis-dev/.tools/compose/cattle-api.yml` with the service host names
replaced by `localhost` and the published ports. The `appsettings.Development.json` settings for
the fake client credentials and CTS apply as they are. `ApiKeyAuthentication__Keys__0` is the key
`lis-be4fe-cattle-home` sends. `lis-be4fe-cattle-home` in the stack
reaches the API at `host.docker.internal:8091`, so it needs no change.
When you finish, run `docker compose up -d lis-api-cattle` to go back to the container.

## Running standalone

The repo has its own compose files for running the API without lis-dev. They contain Postgres,
the Liquibase migration and LocalStack (plus a Redis the API does not use), and they are not part of the lis-dev stack.

| File | Purpose |
|---|---|
| `development-compose.yml` | LocalStack, Redis, Postgres (host port 5433), Liquibase (applies `./changelog`) and the API built from the `Dockerfile` (host port 8085). Run with `docker compose -f development-compose.yml up --build`. |
| `compose.override.yml` | The same services on different ports so they can run beside the lis-dev stack: LocalStack on 4567, Redis on 6380, Postgres on 5432 and the API on 3001. Run with `docker compose -f compose.override.yml up --build`. |
| `compose.yml` | The CDP service template (floci, Redis, MongoDB and a `your-backend` service). It is not used by this API. Do not run it. |

Both working files point KRDS and CADS at `host.docker.internal:3000`, so start a fake service
there (the `lis-fake-service` image) or the upstream calls will fail.

To run just the API from the command line with the stack from `development-compose.yml`:

```bash
docker compose -f development-compose.yml up -d localstack postgres liquibase
export ConnectionStrings__ReadWritePostgresConnection="User ID=lis_api_cattle_ddl;Password=postgres;Host=localhost;Port=5433;Database=lis_cattle;"
export ConnectionStrings__ReadOnlyPostgresConnection="$ConnectionStrings__ReadWritePostgresConnection"
dotnet run --project src/Api --launch-profile http
```

`appsettings.Development.json` points at port 5432 and database `lis_api_cattle`, so the connection
strings above are needed to reach the Postgres in `development-compose.yml`. The `http` profile listens on <http://localhost:8091>. The API serves Scalar for the OpenAPI
documents, and the `.http` files in `tests/Endpoints` exercise each endpoint.

### Running locally against CDP dev

To test the API from a developer machine against the operational KRDS and CADS services in CDP dev, call them through
the `ephemeral-protected` gateway. Every gateway call needs a
[CDP developer API key](https://eaflood.atlassian.net/wiki/spaces/LDD/pages/6449987795/CDP+developer+API+key)
(from the CDP portal) as `x-api-key`, as well as the usual KRDS or CADS Basic credentials. Set `GatewayApiKey` and the
service sends the header on every upstream call. Leave it unset in CDP, where services call each other directly, and
against lis-fake-service.

Export the settings in your shell, or keep them in an untracked file you `source`. Never commit them. Run without a
launch profile: the `http` profile sets the fake-service URLs and credentials, and they override exported variables.
Postgres and LocalStack must be running, as for any local run (for example the
`development-compose.yml` services above).

```bash
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__ReadWritePostgresConnection="User ID=lis_api_cattle_ddl;Password=postgres;Host=localhost;Port=5433;Database=lis_cattle;"
export ConnectionStrings__ReadOnlyPostgresConnection="$ConnectionStrings__ReadWritePostgresConnection"
export ApiKeyAuthentication__Keys__0=local-dev-cattle-api-key
export KrdsApi__BaseUrl=https://ephemeral-protected.api.dev.cdp-int.defra.cloud/ls-keeper-data-api/
export KrdsApi__ClientId=<KRDS ACL client id>
export KrdsApi__ClientSecret=<KRDS ACL client secret>
export KrdsApi__GatewayApiKey=<CDP developer API key>
export CadsApi__BaseUrl=https://ephemeral-protected.api.dev.cdp-int.defra.cloud/cads-data-service/
export CadsApi__ClientId=<CADS client id>
export CadsApi__ClientSecret=<CADS client secret>
export CadsApi__GatewayApiKey=<CDP developer API key>
dotnet run --project src/Api --no-launch-profile --urls http://localhost:5019
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

### Configuration

Defaults are in `src/Api/appsettings.json`. CDP sets non-secret values from
[cdp/app-config](https://github.com/DEFRA/cdp-app-config) (`services/lis-api-cattle`). Values marked
secret must be set as secrets in the CDP portal for every deployed environment.

| Variable | Description | Default |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` loads `appsettings.Development.json` | `Production` |
| `ASPNETCORE_URLS` | Address to bind | `http://+:${PORT}` in the Docker image; compose sets `http://+:8085` |
| `PostgresConfiguration__UseIamAuthentication` | Use IAM authentication to Aurora | `true` |
| `PostgresConfiguration__ReadWriteHost` | Read-write database host | set in CDP |
| `PostgresConfiguration__ReadOnlyHost` | Read-only database host | set in CDP |
| `PostgresConfiguration__Port` | Database port | `5432` |
| `PostgresConfiguration__Name` | Database name | set in CDP |
| `PostgresConfiguration__User` | Database user | set in CDP |
| `ConnectionStrings__ReadWritePostgresConnection` | Local only. Read-write connection string, used when IAM authentication is off | none |
| `ConnectionStrings__ReadOnlyPostgresConnection` | Local only. Read-only connection string | none |
| `AWS__Region` | AWS region | `eu-west-2` |
| `AWS__UseLocalStack` | Use `AWS__ServiceUrl` instead of AWS | `false` |
| `AWS__ServiceUrl` | LocalStack endpoint | none |
| `AWS__SubmissionValidationQueueUrl` | SQS queue for submission validation | none (set in CDP) |
| `AWS__SubmissionValidationTopicArn` | Optional SNS topic for validation events | none |
| `AWS__EnableBackgroundConsumer` | Run the queue consumers (validation and registration bundles) | `true` |
| `AWS__PollingIntervalSeconds` | Queue polling interval | `10` |
| `AWS__MaxNumberOfMessages` | Messages read per poll | `10` |
| `AWS__WaitTimeSeconds` | SQS long-poll wait | `5` |
| `AWS__RegistrationBundleQueueUrl` | SQS queue for registration bundles. Not the validation queue | none (set in CDP) |
| `AWS__EnableRegistrationBundleConsumer` | Store queued bundles and send them to CTS | `true` |
| `AWS__RegistrationBundleVisibilityTimeoutSeconds` | How long a received bundle is hidden from other instances. Longer than the worst-case processing time | `300` |
| `AWS__RegistrationBundleMaxReceiveCount` | The queue's redrive `maxReceiveCount`, used to log the last attempt | `5` |
| `SubmissionValidation__MinDamAgeInMonths` | Minimum dam age | `15` |
| `SubmissionValidation__MaxDamAgeInYears` | Maximum dam age | `20` |
| `SubmissionValidation__MinCalvingIntervalDays` | Minimum days between calvings | `240` |
| `SubmissionValidation__MaxApplicationLateDays` | Latest days after birth to apply | `27` |
| `CtsPollingJob__Enabled` | Run the CTS polling job | `true` |
| `CtsPollingJob__CronSchedule` | Quartz cron schedule | `0/30 * * * * ?` |
| `CtsPollingJob__PollingIntervalSeconds` | Polling interval | `30` |
| `CtsPollingJob__BatchSize` | Bundles claimed per run | `10` |
| `CtsPollingJob__UnknownReceiptMaxAgeHours` | Hours after sending that a bundle whose receipt CTS does not recognise is marked `error` | `24` |
| `CtsWeb__Url` | CTS Web Services `DefraDataTransferPublicNWSE.asmx` URL | `http://lis-fake-service/cts_ws/DefraDataTransferPublicNWSE.asmx` |
| `CtsWeb__ServiceName` | CTS service name in the `TransferDataHex` envelope | set in CDP |
| `CtsWeb__DthUsername` | Secret. `TransferDataHex` user name | empty |
| `CtsWeb__DthPassword` | Secret. `TransferDataHex` password (sent MD5-hashed) | empty |
| `CtsWeb__CtsOnlineUsername` | Secret. CTS Online system account user name | empty |
| `CtsWeb__CtsOnlinePassword` | Secret. CTS Online system account password | empty |
| `CtsWeb__TimeoutMilliseconds` | Timeout for one CTS call | `30000` |
| `CtsWeb__MaxAttempts` | Attempts when CTS is unavailable, before the bundle is marked as errored | `3` |
| `CtsWeb__RetryDelayMilliseconds` | Delay before the second attempt, doubling after each | `1000` |
| `KrdsApi__BaseUrl` | KRDS base URL, with trailing slash | `http://lis-fake-service/krds/` |
| `KrdsApi__ClientId` | KRDS Basic client ID | empty |
| `KrdsApi__ClientSecret` | Secret. KRDS Basic client secret | empty |
| `KrdsApi__GatewayApiKey` | Secret. CDP developer API key, for the dev gateway only | none |
| `KrdsApi__GetHoldingDetailsTimeout` | Timeout in ms for get holding details | `60000` |
| `KrdsApi__SearchHoldingsTimeout` | Timeout in ms for search holdings | `60000` |
| `KrdsApi__GetUserAccountTimeout` | Timeout in ms for get user account | `60000` |
| `KrdsApi__EnsureUserAccountTimeout` | Timeout in ms for ensure user account | `60000` |
| `KrdsApi__CircuitBreakerConsecutiveFailureThreshold` | Consecutive failure threshold for KRDS circuit breaker | `5` |
| `KrdsApi__CircuitBreakerFailureRatio` | Failure ratio threshold for KRDS circuit breaker | `0.5` |
| `KrdsApi__CircuitBreakerSamplingDuration` | Sampling duration in ms for KRDS circuit breaker | `30000` |
| `KrdsApi__CircuitBreakerDurationOfBreak` | Duration of break in ms for KRDS circuit breaker | `30000` |
| `KrdsApi__CircuitBreakerMinimumThroughput` | Minimum throughput for KRDS circuit breaker | `10` |
| `CadsApi__BaseUrl` | CADS base URL, with trailing slash | `http://lis-fake-service/cads/` |
| `CadsApi__ClientId` | CADS Basic client ID | empty |
| `CadsApi__ClientSecret` | Secret. CADS Basic client secret | empty |
| `CadsApi__GatewayApiKey` | Secret. CDP developer API key, for the dev gateway only | none |
| `CadsApi__PageSize` | CADS page size | `500` |
| `CadsApi__GetAnimalsOnHoldingTimeout` | Timeout in ms for get animals on holding | `60000` |
| `CadsApi__GetAnimalDetailsTimeout` | Timeout in ms for get animal details | `60000` |
| `CadsApi__CircuitBreakerConsecutiveFailureThreshold` | Consecutive failure threshold for CADS circuit breaker | `5` |
| `CadsApi__CircuitBreakerFailureRatio` | Failure ratio threshold for CADS circuit breaker | `0.5` |
| `CadsApi__CircuitBreakerSamplingDuration` | Sampling duration in ms for CADS circuit breaker | `30000` |
| `CadsApi__CircuitBreakerDurationOfBreak` | Duration of break in ms for CADS circuit breaker | `30000` |
| `CadsApi__CircuitBreakerMinimumThroughput` | Minimum throughput for CADS circuit breaker | `10` |
| `CadsDataPatch__BucketName` | S3 bucket for the temporary CADS data patch | empty |
| `ApiKeyAuthentication__Keys__0` | Secret. Accepted inbound API key | none |
| `ApiKeyAuthentication__Keys__1` | Secret. Optional second key for rotation | none |
| `Serilog__MinimumLevel__Default` | Log level | `Information` |

## Running tests

```bash
dotnet test Cattle.slnx          # all tests, with coverage
dotnet format --verify-no-changes Cattle.slnx    # formatting
```

The solution has one test project, `tests/Api.Tests` (xUnit). Most tests are unit tests. The
tests in `tests/Api.Tests/Integration` start a Postgres container with Testcontainers and apply
the Liquibase schema, so they need Docker running. The `.http` files in `tests/Endpoints` are for
trying the API by hand. StyleCop analysers run during the build, and warnings are reported by the
build and by Sonar.

End-to-end journeys are covered by [lis-journey-tests](https://github.com/DEFRA/lis-journey-tests).

## Monitoring

- Health check: `GET /health` is the platform's liveness probe and only confirms the service is running, so a database or queue outage does not get every instance restarted. `GET /health/detailed` checks the database, the queue and the Quartz scheduler, and returns `503` when any of them is unhealthy or degraded. Both are anonymous.
- Logs: OpenSearch dashboard per environment, linked from the [CDP portal service page](https://portal.cdp-int.defra.cloud/services/lis-api-cattle) under Logs
- Metrics: Grafana dashboard per environment, linked from the [CDP portal service page](https://portal.cdp-int.defra.cloud/services/lis-api-cattle) under Metrics

Logs are ECS JSON written with Serilog and carry the `CorrelationId`.

## Pipelines

Build, versioning and publishing run in GitHub Actions (`.github/workflows`) using Cake scripts (`*.cake`):

- **Pull request to `main`**: validate (format, build, test), Sonar analysis, version check
- **Push to a `feature/` or `hotfix/` branch**: prerelease build and tag
- **Merge to `main`**: release tag, then publish the container image and the database migration to CDP

Versions come from git tags (SemVer).

## Branching policy

The repo uses mainline development. Branch from `main` and name the branch:

- `feature/LREG-<ticket>-<short-kebab-description>`
- `hotfix/LREG-<ticket>-<short-kebab-description>`

`LREG` must be uppercase, because the GitHub ruleset rejects other branch names. Rebase onto `main`
rather than merging `main` in. Incomplete work goes behind a feature flag.

## Contributing to this project

- Follow the [Defra software development standards](https://github.com/DEFRA/software-development-standards).
- Follow [CODINGSTANDARDS.md](CODINGSTANDARDS.md) (minimal API endpoints in `Endpoints/`, domain-driven design, StyleCop).
- Add a Liquibase changeset under [changelog/](changelog) for any schema change.
- Run `dotnet format Cattle.slnx` and `dotnet test Cattle.slnx` before you push.
- Open a pull request against `main` using the PR template. CI must pass and the Sonar quality gate must be green before merge.

## Licence

THIS INFORMATION IS LICENSED UNDER THE CONDITIONS OF THE OPEN GOVERNMENT LICENCE found at:

<http://www.nationalarchives.gov.uk/doc/open-government-licence/version/3>

The following attribution statement MUST be cited in your products and applications when using this information.

> Contains public sector information licensed under the Open Government licence v3

### About the licence

The Open Government Licence (OGL) was developed by the Controller of His Majesty's Stationery Office (HMSO) to enable information providers in the public sector to license the use and re-use of their information under a common open licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
