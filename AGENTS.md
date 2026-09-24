# AGENTS.md

Guidance and instructions for AI coding agents (Junie, Claude, Codex, Cursor, etc.) and human developers working in this repository.

---

## ⚠️ Mandatory Coding Standards & DDD Architecture

All AI agents and developers working in this repository **must strictly adhere** to the guidelines documented in **[CODINGSTANDARDS.md](CODINGSTANDARDS.md)** before designing, refactoring, or implementing any features or endpoints.

Key imperatives from **[CODINGSTANDARDS.md](CODINGSTANDARDS.md)** include:
- **ASP.NET Minimal APIs**: All endpoints must follow the Minimal API pattern and be separated into dedicated files under the `Endpoints/` directory (e.g. `src/Api/Endpoints/`). Endpoints must **not** be inlined or interweaved in `Program.cs`.
- **Domain-Driven Design (DDD)**:
  - **Rich Domain Models**: Avoid anemic models; encapsulate invariants and state transitions in Entity/Aggregate Root methods.
  - **Aggregate Root Integrity**: Maintain consistency boundaries; mutations must flow through Aggregate Roots.
  - **Immutable Value Objects**: Use `record` or immutable structures for attributes without conceptual identity.
  - **Persistence Ignorance**: Keep domain entities as POCOs with no EF Core dependencies.
  - **Layered Architecture**: Strict dependency flow (`Api` -> `Database` -> `Entities` / `Domain`).

---

## Project Overview

**LIS API Cattle** (`lis-api-cattle`) is a backend service developed for DEFRA Livestock Information Service (LIS), handling cattle submission data, validation, movement/tag events, and integration with CADS and AWS services.

- **Target Framework**: .NET 10 (`net10.0`)
- **Language**: C# 14
- **Architecture**: Domain-Driven Design (DDD) & Clean/Layered Architecture with ASP.NET Core Minimal APIs
- **Database**: PostgreSQL (managed via Liquibase changelogs and EF Core)
- **Messaging & Cloud**: AWS SDK (SQS, RDS, SecretsManager) & LocalStack for local development

---

## Solution Layout & Directory Structure

```
lis-api-cattle/
├── Cattle.slnx                       # Solution file
├── Cattle.sln.DotSettings.user       # JetBrains Rider / ReSharper settings
├── Directory.Build.props              # Common MSBuild properties
├── Directory.Packages.props           # Central Package Management (CPM) versions
├── global.json                        # .NET SDK pinning
├── Dockerfile                         # Application container definition
├── development-compose.yml            # Docker Compose setup for local development
├── compose.override.yml               # Local compose overrides
├── CODINGSTANDARDS.md                 # Mandatory C# and DDD coding standards
├── AGENTS.md                          # Guidance for AI coding agents (this file)
│
├── src/                               # Application source code
│   ├── Entities/                      # Domain Entities & Models (Entities.csproj)
│   │   ├── Submission.cs              # Submission aggregate / entity
│   │   ├── SubmissionAnimal.cs        # Animal submission entity
│   │   └── SubmissionAnimalError.cs   # Submission validation / error entity
│   │
│   ├── Database/                      # Persistence & Infrastructure (Database.csproj)
│   │   └── Configurations/            # EF Core Entity Configurations (IEntityTypeConfiguration<T>)
│   │       ├── SubmissionConfiguration.cs
│   │       ├── SubmissionAnimalConfiguration.cs
│   │       └── SubmissionAnimalErrorConfiguration.cs
│   │
│   └── Api/                           # Web API & Presentation Layer (Api.csproj)
│       ├── Exceptions/                # Exception handlers and logging
│       ├── Interfaces/                # Service contracts (ICattleService, ICadsService)
│       ├── Models/                    # DTOs, API request & response models
│       ├── Services/                  # Application services (CattleService, CadsService)
│       ├── Endpoints/                 # (Target location) Minimal API route definitions
│       ├── appsettings.json           # Configuration settings
│       ├── appsettings.Development.json
│       └── Program.cs                 # API bootstrap, DI registration, middleware pipeline
│
├── tests/                             # Automated test suites
│   └── Api.Tests/                     # API & Unit tests (Api.Tests.csproj)
│       └── CattleServiceTests.cs      # Tests for cattle service logic
│
├── changelog/                         # Liquibase database schema & migrations
│   ├── db.changelog.xml               # Master Liquibase changelog
│   ├── liquibase.properties           # Liquibase configuration
│   └── schema/                        # SQL migration scripts
│       └── 01/
│           └── initial.sql            # Initial PostgreSQL database schema
│
└── compose/                           # Local environment orchestration
    ├── aws.env                        # AWS environment variables for local testing
    ├── start-localstack.sh            # Script to initialize LocalStack
    └── start-localstack-override.sh
```

---

## Projects & Responsibilities

### 1. `src/Entities` (`Entities.csproj`)
- Contains domain entities and business objects (e.g., `Submission`, `SubmissionAnimal`, `SubmissionAnimalError`).
- Serves as the core Domain layer.
- Must remain decoupled from persistence and presentation frameworks.

### 2. `src/Database` (`Database.csproj`)
- Contains Entity Framework Core configurations and database contexts.
- Maps domain entities to PostgreSQL tables using Fluent API (`IEntityTypeConfiguration<T>`).
- References `Entities.csproj`.

### 3. `src/Api` (`Api.csproj`)
- Entry point for the ASP.NET Core service.
- Houses Minimal API endpoint mappings, application services, service interfaces, custom exception handlers, and DTOs.
- `Program.cs` manages DI registration and middleware; route definitions must live in dedicated files in `src/Api/Endpoints/`.
- References `Database.csproj` and `Entities.csproj`.

### 4. `tests/Api.Tests` (`Api.Tests.csproj`)
- Unit and integration tests using xUnit, Moq, and EF Core In-Memory / Testcontainers.
- Tests business logic in application services and API contracts.

### 5. `changelog/`
- Manages PostgreSQL schema versions via Liquibase.
- All database schema modifications must have corresponding changelog entries.

---

## Upstream connections (CADS and KRDS)

Holding details, animals-on-holding and single-animal details are read from upstream services
through REST strategies (`Defra.Livestock.Sdk.Api.Strategies`): `src/Api/Services/KrdsService.cs`
calls the keeper-data-api V2 holdings endpoint and `src/Api/Services/CadsService.cs` calls the CADS
bovine animals endpoints (`api/v1/bovine/animals` for a holding, `api/v1/bovine/animals/{identifier}`
for one animal).
Until the real services exist both point at [lis-fake-service](https://github.com/DEFRA/lis-fake-service)
(`npm run dev`, port 3000), which requires HTTP Basic credentials per upstream.

| Setting | Purpose | Local default |
| --- | --- | --- |
| `CadsApi__BaseUrl` | Base URL of the CADS API, including any path prefix (fake: `http://localhost:3000/cads/`) | `appsettings.Development.json` |
| `CadsApi__ClientId` / `CadsApi__ClientSecret` | Basic credentials for CADS (fake defaults `local-dev-cads-client` / `local-dev-cads-secret`) | dev settings; CDP secrets elsewhere |
| `CadsApi__PageSize` | Page size used when reading animals (all pages are read) | 100 |
| `KrdsApi__BaseUrl` | Base URL of the keeper-data-api, including any path prefix (fake: `http://localhost:3000/krds/`) | `appsettings.Development.json` |
| `KrdsApi__ClientId` / `KrdsApi__ClientSecret` | Basic credentials for KRDS (fake defaults `local-dev-krds-client` / `local-dev-krds-secret`) | dev settings; CDP secrets elsewhere |

The services send only the API-relative paths (`api/v1/bovine/animals`, `api/v1/bovine/animals/{identifier}`,
`api/v2/holdings/...`), so any
route qualifier such as the fake service's `/cads` and `/krds` prefixes must be part of the base URL, never the code.

All settings are validated when an upstream call is first made (not on start-up, so `/health` works before the secrets are set). The inbound `x-cdp-request-id` header is propagated to
every upstream call (Correlation ID standard). Never log the credentials or upstream payloads.

Endpoints exposed for the BE4FE: `GET /holdings/{county}/{parish}/{holding}`,
`GET /holdings/{county}/{parish}/{holding}/cattle?earTag=&breed=&sex=` (live animals only) and
`GET /cattle/{earTag}` (details for one animal, 404 when CADS does not know it). See
`tests/Endpoints/Cattle/Cattle.http`.

`GET /cattle/{earTag}` flattens the parentage list CADS returns into `damType` (`surrogate` when a
surrogate dam is recorded, otherwise `genetic` when a genetic dam is), `geneticDamEarTag`,
`surrogateDamEarTag` and `sireEarTag`. `sireName` is always null: CADS does not carry one. The
response is CADS-only, so an animal that exists solely in a local submission bundle is a 404 here
even though it appears in the holding's cattle list.

---

## Development & Build Commands

- **Build Solution**:
  ```bash
  dotnet build Cattle.slnx
  ```
- **Run Tests**:
  ```bash
  dotnet test Cattle.slnx
  ```
- **Run API Project**:
  ```bash
  dotnet run --project src/Api/Api.csproj
  ```
- **Format Code**:
  ```bash
  dotnet format
  ```

---

## Guidelines for AI Agents

1. **Review Standards First**: Read `CODINGSTANDARDS.md` before generating or modifying C# code.
2. **Minimal API Endpoints**: When adding new API endpoints, create a new file under `src/Api/Endpoints/` and create an extension method on `IEndpointRouteBuilder`. Hook the extension method in `Program.cs`.
3. **Domain Modeling**: Ensure entities protect invariants and encapsulate state changes. Do not create anemic models with public getters and setters.
4. **Central Package Management**: Do not specify package versions in individual `.csproj` files; add or update versions in `Directory.Packages.props`.
5. **Testing**: Write unit tests in `tests/Api.Tests/` for any new business logic, service methods, or endpoint workflows. Ensure all tests pass (`dotnet test`) before concluding work.
