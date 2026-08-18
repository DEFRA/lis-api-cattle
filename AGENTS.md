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
