# lis-api-cattle

Part of the Livestock Information Service (LIS), which lets livestock keepers, owners, agents and
delegates record and manage their animals.

The cattle API is the backend for cattle in LIS. It is a .NET 10 minimal API that returns holdings
and the cattle on them, creates and validates cattle registration submissions, and keeps the
accounts of signed-in users. It stores submissions in Postgres, reads holding and animal data from
KRDS and CADS, and polls for submitted bundles to send to CTS (the Cattle Tracing System).

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
  [Correlation ID](#correlation-id)). `/health`, the OpenAPI document and Scalar are anonymous.
- Downstream calls:
  - KRDS (keeper and holding data) and CADS (animals on a holding) over HTTP, each with Basic
    credentials (`ClientId` and `ClientSecret`).
  - CTS: only a fake client exists (`CtsApi__UseFake`). See [Messaging](#messaging).
  - AWS SQS and SNS (submission validation) and S3 (the temporary CADS data patch).
  - Postgres, through Entity Framework Core.

### Endpoints

Every `/v1` endpoint requires the `x-api-key` and `x-cdp-request-id` headers. Scalar serves
interactive documentation, and the OpenAPI document is published per API version.

| Method and path | Purpose |
|---|---|
| `GET /v1/holdings` | Search holdings |
| `GET /v1/holdings/{county}/{parish}/{holding}` | Get the holding details |
| `GET /v1/holdings/{county}/{parish}/{holding}/cattle` | Cattle on the holding |
| `GET /v1/holdings/{cph}/cattle` | Cattle on the holding, by CPH |
| `GET /v1/holdings/{county}/{parish}/{holding}/bundles` | Registration bundles for the holding |
| `GET /v1/holdings/{cph}/bundles` | Registration bundles for the holding, by CPH |
| `GET /v1/cattle/{earTag}` | Get the cattle details |
| `POST /v1/registrations` | Create a cattle registration bundle |
| `POST /v1/registrations/{id}/validate` | Validate a cattle registration bundle |
| `GET /v1/users/{subject}` | Get a user's details and associated CPHs |
| `POST /v1/users` | Create or refresh a user account |
| `GET /health` | Health check (anonymous) |
| `GET /health/detailed` | Detailed health status (anonymous) |

### Service-to-service authentication

Every versioned endpoint (`/v1/...`) requires an `x-api-key` header that matches one of the configured keys,
otherwise it returns `401` problem details. `/health`, the OpenAPI document and Scalar stay anonymous. This is the
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
BE4FE always send it; `/health`, the OpenAPI document and Scalar do not need it. The value is:

- echoed on the response and included as `correlationId` in error problem details;
- written as `CorrelationId` on every log line (ECS JSON in CDP, plain text locally);
- forwarded on every upstream call (KRDS, CADS, CTS).

The `.http` files send a fresh `{{$guid}}`; send your own when calling the API by hand, for example
`-H 'x-cdp-request-id: local-test-1'`.

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

Submissions are validated asynchronously.

- `SubmissionMessagePublisher` sends each submission to the SQS queue in `AWS__SubmissionValidationQueueUrl`
  and, when `AWS__SubmissionValidationTopicArn` is set, publishes the same message to an SNS topic.
  A failed SNS publish is logged and does not fail the request.
- `SubmissionValidationBackgroundService` polls the queue and validates each submission. Set
  `AWS__EnableBackgroundConsumer` to `false` to turn it off.
- `CtsBundlePollingJob` (Quartz) claims submitted bundles from Postgres on a schedule and passes
  them to the CTS client. Only a fake CTS client exists, so the service refuses to start when
  `CtsApi__UseFake` is `false`.

Queue and topic naming rules, and the local and planned names, are in [docs/messaging.md](docs/messaging.md).

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
| `AWS__EnableBackgroundConsumer` | Poll the validation queue | `true` |
| `AWS__PollingIntervalSeconds` | Queue polling interval | `10` |
| `AWS__MaxNumberOfMessages` | Messages read per poll | `10` |
| `AWS__WaitTimeSeconds` | SQS long-poll wait | `5` |
| `SubmissionValidation__MinDamAgeInMonths` | Minimum dam age | `15` |
| `SubmissionValidation__MaxDamAgeInYears` | Maximum dam age | `20` |
| `SubmissionValidation__MinCalvingIntervalDays` | Minimum days between calvings | `240` |
| `SubmissionValidation__MaxApplicationLateDays` | Latest days after birth to apply | `27` |
| `CtsPollingJob__Enabled` | Run the CTS polling job | `true` |
| `CtsPollingJob__CronSchedule` | Quartz cron schedule | `0/30 * * * * ?` |
| `CtsPollingJob__PollingIntervalSeconds` | Polling interval | `30` |
| `CtsPollingJob__BatchSize` | Bundles claimed per run | `10` |
| `CtsApi__UseFake` | Use the fake CTS client. Must be `true` for now | `true` |
| `KrdsApi__BaseUrl` | KRDS base URL, with trailing slash | `http://lis-fake-service/krds/` |
| `KrdsApi__ClientId` | KRDS Basic client ID | empty |
| `KrdsApi__ClientSecret` | Secret. KRDS Basic client secret | empty |
| `KrdsApi__GatewayApiKey` | Secret. CDP developer API key, for the dev gateway only | none |
| `KrdsApi__GetHoldingDetailsTimeout` | Timeout in ms for get holding details | `60000` |
| `KrdsApi__SearchHoldingsTimeout` | Timeout in ms for search holdings | `60000` |
| `KrdsApi__GetUserAccountTimeout` | Timeout in ms for get user account | `60000` |
| `KrdsApi__EnsureUserAccountTimeout` | Timeout in ms for ensure user account | `60000` |
| `CadsApi__BaseUrl` | CADS base URL, with trailing slash | `http://lis-fake-service/cads/` |
| `CadsApi__ClientId` | CADS Basic client ID | empty |
| `CadsApi__ClientSecret` | Secret. CADS Basic client secret | empty |
| `CadsApi__GatewayApiKey` | Secret. CDP developer API key, for the dev gateway only | none |
| `CadsApi__PageSize` | CADS page size | `500` |
| `CadsApi__GetAnimalsOnHoldingTimeout` | Timeout in ms for get animals on holding | `60000` |
| `CadsApi__GetAnimalDetailsTimeout` | Timeout in ms for get animal details | `60000` |
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

- Health check: `GET /health` checks the database, the queue and the Quartz scheduler. `GET /health/detailed` shows the same in more detail. Both are anonymous.
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
