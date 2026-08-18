# C# Coding Standards & Architecture Guidelines

This document outlines the architectural principles and C# coding standards for agents and developers working on this codebase. All contributions must adhere to these guidelines to ensure maintainability, testability, consistency, and alignment with business requirements.

---

## 1. ASP.NET Core Minimal APIs & Endpoint Organization

### 1.1 Minimal APIs Structure
- **No Interweaving in `Program.cs`**: Endpoints must not be defined directly within `Program.cs`. `Program.cs` should only be responsible for service registration (dependency injection), middleware pipeline configuration, and endpoint route group mapping.
- **Dedicated `Endpoints/` Folder**: All API routes and endpoints must be organized into separate files located within the `Endpoints/` folder (e.g., `src/Api/Endpoints/`).
- **Endpoint Definitions**:
  - Group related endpoints by resource / bounded context / feature into static classes or extension methods (e.g., implementing an endpoint mapping extension method like `MapCattleEndpoints(this IEndpointRouteBuilder app)`).
  - Use typed results (`Results.Ok()`, `Results.NotFound()`, `Results.Created()`, `TypedResults`) and status codes.
  - Keep endpoint handlers focused on request validation, invoking application / domain services, and returning appropriate HTTP responses. Do not place core domain logic inside endpoint handlers.

#### Example Endpoint Organization

```csharp
// src/Api/Endpoints/CattleEndpoints.cs
namespace Api.Endpoints;

public static class CattleEndpoints
{
    public static IEndpointRouteBuilder MapCattleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cattle")
                       .WithTags("Cattle");

        group.MapGet("/", GetCattleList)
             .WithName("GetCattleList")
             .Produces<IReadOnlyCollection<CattleDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetCattleById)
             .WithName("GetCattleById")
             .Produces<CattleDto>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateCattle)
             .WithName("CreateCattle")
             .Produces<CattleDto>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetCattleList(ICattleService cattleService, CancellationToken ct)
    {
        var result = await cattleService.GetAllAsync(ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCattleById(Guid id, ICattleService cattleService, CancellationToken ct)
    {
        var result = await cattleService.GetByIdAsync(id, ct);
        return result is not null ? Results.Ok(result) : Results.NotFound();
    }

    private static async Task<IResult> CreateCattle(CreateCattleRequest request, ICattleService cattleService, CancellationToken ct)
    {
        var created = await cattleService.CreateAsync(request, ct);
        return Results.CreatedAtRoute("GetCattleById", new { id = created.Id }, created);
    }
}
```

In `Program.cs`:
```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// ... register domain, application, and infrastructure services ...

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map endpoint modules cleanly
app.MapCattleEndpoints();

app.Run();
```

---

## 2. Domain-Driven Design (DDD) Principles

Domain-Driven Design (DDD) in C# requires strict adherence to strategic design (bounded contexts) and tactical patterns (aggregates, entities, value objects) to ensure the codebase accurately reflects business logic.

### 2.1 Layered Architecture
Organize code into distinct, decoupled layers:
- **Domain Layer**: Contains core domain logic, Aggregates, Entities, Value Objects, Domain Events, Domain Exceptions, and Repository Interfaces.
  - **Rule**: The Domain layer must have zero dependencies on Application or Infrastructure layers, as well as external frameworks (e.g., EF Core, ASP.NET Core).
- **Application Layer**: Contains use cases, command and query handlers, DTOs, interfaces for external dependencies, and orchestrates domain operations.
- **Infrastructure Layer**: Implements repository interfaces, database persistence (Entity Framework Core / Dapper / SQL), third-party service clients, file storage, message brokers, and logging.
- **Presentation / API Layer**: ASP.NET Minimal API endpoints, middleware, filters, and configuration.

### 2.2 Rich Domain Models vs. Anemic Domain Models
- **Avoid Anemic Domain Models**: Entities must never be mere property bags with public getters and setters.
- **Encapsulate Business Rules and Invariants**: Business rules, invariants, and validation must live inside Entity methods.
- **Read-Only State**: Expose properties with private/protected setters or as read-only getters. State transitions must occur through explicit, intent-revealing domain methods (e.g., `order.MarkAsShipped()`, `animal.RegisterTag(tagNumber)`).

### 2.3 Aggregate Root Integrity
- **Single Entry Point**: The Aggregate Root is the only entry point for modifying any entity within the aggregate boundary.
- **Consistency Boundaries**: Application layer services must interact exclusively with the Aggregate Root. Child entities must never be updated directly from outside the aggregate boundary.
- **Encapsulated Collections**: Expose internal collections as `IReadOnlyCollection<T>` or `IReadOnlyList<T>` (e.g., via `_items.AsReadOnly()`), preventing external modifications.

### 2.4 Immutable Value Objects
- **Value Semantics**: Value Objects represent concepts defined strictly by their attributes rather than a unique identity.
- **Immutability**: Value objects must be completely immutable after instantiation.
- **Implementation in C#**:
  - Prefer C# `record` types (or `readonly record struct` / immutable classes) for concise value semantics, structural equality (`Equals`, `GetHashCode`), and immutability.
  - Throw domain validation exceptions in constructors/factories when invariant checks fail.

```csharp
public sealed record EarTag
{
    public string CountryCode { get; }
    public string Identifier { get; }

    public EarTag(string countryCode, string identifier)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
            throw new DomainValidationException("Country code must be a valid 2-letter ISO code.");

        if (string.IsNullOrWhiteSpace(identifier))
            throw new DomainValidationException("Identifier cannot be empty.");

        CountryCode = countryCode.ToUpperInvariant();
        Identifier = identifier.Trim();
    }
}
```

### 2.5 Persistence Ignorance
- **POCO Entities**: Domain entities must be Plain Old CLR Objects (POCOs) free from infrastructure-specific base classes or persistence attributes (e.g., no EF Core annotations like `[Table]`, `[Column]`, `[Key]`).
- **Domain Interfaces**: Define repository contracts (e.g., `ICattleRepository`, `IOrderRepository`) within the Domain or Application layer.
- **Infrastructure Mapping**: EF Core mappings must be configured using `IEntityTypeConfiguration<T>` in the Infrastructure layer using Fluent API, keeping the domain completely persistence-ignorant.

### 2.6 Ubiquitous Language
- **Mirror Business Domain**: Class names, method names, variable names, and domain events must strictly mirror the Ubiquitous Language shared with domain experts and stakeholders.
- **Executable Documentation**: Code should read like executable business requirements (e.g., `cattle.RecordMovement(fromHolding, toHolding, movementDate)` instead of `cattle.UpdateHoldingId(id)`).

---

## 3. DDD Implementation Example

Below is a typical Aggregate Root pattern enforcing behavior over state exposure:

```csharp
namespace Domain.Aggregates.OrderAggregate;

using Domain.Common;
using Domain.Exceptions;

public class Order : Entity, IAggregateRoot
{
    private readonly List<OrderItem> _orderItems = [];

    // Private parameterless constructor for ORM reflection if needed
    private Order() { }

    public Order(Guid customerId, Address shippingAddress)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId != Guid.Empty ? customerId : throw new DomainValidationException("Customer ID is required.");
        ShippingAddress = shippingAddress ?? throw new ArgumentNullException(nameof(shippingAddress));
        Status = OrderStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid CustomerId { get; private set; }
    public Address ShippingAddress { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ShippedAtUtc { get; private set; }

    // Read-only collection to prevent external modification
    public IReadOnlyCollection<OrderItem> OrderItems => _orderItems.AsReadOnly();

    public decimal TotalAmount => _orderItems.Sum(item => item.TotalPrice);

    public void AddOrderItem(int productId, string productName, decimal unitPrice, decimal discount, string pictureUrl, int units = 1)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainInvalidOperationException("Cannot add items to an order that is not in draft status.");

        if (units <= 0)
            throw new DomainValidationException("Units must be greater than zero.");

        var existingItem = _orderItems.SingleOrDefault(i => i.ProductId == productId);
        if (existingItem is not null)
        {
            existingItem.AddUnits(units);
        }
        else
        {
            var orderItem = new OrderItem(productId, productName, unitPrice, discount, pictureUrl, units);
            _orderItems.Add(orderItem);
        }
    }

    public void MarkAsShipped()
    {
        if (Status != OrderStatus.Paid)
            throw new DomainInvalidOperationException("Only paid orders can be marked as shipped.");

        Status = OrderStatus.Shipped;
        ShippedAtUtc = DateTime.UtcNow;
    }
}
```

---

## 4. General C# Code Style & Quality Standards

- **Modern C# Idioms**: Use modern C# language features (pattern matching, file-scoped namespaces, nullable reference types, primary constructors, collection expressions `[]`).
- **Nullable Reference Types**: Keep nullable reference types enabled (`<Nullable>enable</Nullable>`). Avoid null-forgiving operators (`!`) unless strictly necessary and justified.
- **Async/Await**: Ensure asynchronous operations propagate `CancellationToken` throughout call chains. Avoid `Result` or `.Wait()`.
- **Validation**: Place domain-specific validation in aggregate roots / entities / value objects. Place input / request contract validation in application / presentation layer (e.g. FluentValidation).
- **Exceptions**: Use custom domain exceptions (e.g., `DomainValidationException`, `EntityNotFoundException`) for domain invariant violations rather than raw system exceptions.
