# Agent Guidelines for multi-carrier-api

## Project Overview

Clean Architecture .NET solution (v10.8.0) using Aspire for distributed development. Core domain: multi-carrier shipping integration (FedEx, UPS).

## Build & Run

```bash
# Build
dotnet build

# Run
dotnet run --project ./src/AppHost
```

The Aspire dashboard opens automatically showing all services and logs.

## Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test --project tests/Application.UnitTests
```

### Test Categories

- **Unit tests** (`tests/*.UnitTests`): Fast, isolated tests
- **Functional tests** (`tests/Application.FunctionalTests`): Full AppHost simulation with in-memory DB
- **Integration tests** (`tests/Infrastructure.IntegrationTests`): PostgreSQL-backed infrastructure tests

## Architecture

### Layer Dependencies (strict, no cycles)

```
Web → Application → Domain
     ↓
Infrastructure → Domain
```

### Core Layers

| Layer | Purpose | Key Files |
|-------|---------|-----------|
| `src/Domain` | Core entities, value objects, domain events | `Entities/`, `Enums/`, `ValueObjects/`, `Events/` |
| `src/Application` | Use cases, commands, queries, business logic | `Shipments/` (feature), `DependencyInjection.cs` |
| `src/Infrastructure` | Data access, external adapters, identity | `Carriers/` (FedEx/UPS adapters), `Data/`, `Identity/` |
| `src/Web` | ASP.NET Core API, UI layer | `Program.cs`, Controllers |
| `src/AppHost` | Aspire orchestration, service composition | `appsettings.*.json` |
| `src/ServiceDefaults` | Shared service defaults (logging, health, etc.) | - |

## Key Dependencies

- **Database**: PostgreSQL (via Npgsql)
- **Identity**: ASP.NET Core Identity with EF Core
- **CQRS/Mediator**: MediatR with pipeline behaviors
- **Mapping**: AutoMapper
- **External APIs**: FedEx, UPS (configurable adapters)

## Configuration & Environment

The AppHost defines service connections. Check `src/AppHost/appsettings.Development.json` and `aspire.config.json` for connection string patterns.

### Carrier Adapter Configuration

Each carrier has a real adapter and a mock. `Carriers:<Carrier>:UseMock` in `src/Web/appsettings.json` (bound to `CarriersOptions`) picks which one is registered. Both default to `true`. The real adapters also need `AccountNumber`, `ClientId` and `ClientSecret`, which come from user secrets.

## Code Scaffolding

Install template if needed:

```bash
dotnet new install Clean.Architecture.Solution.Template::10.8.0
```

Create new features in `./src/Application/`:

```bash
# Command
dotnet new ca-usecase --name CreateX --feature-name FeatureName --usecase-type command --return-type int

# Query
dotnet new ca-usecase -n GetX -fn FeatureName -ut query -rt XVm
```

## Coding Conventions

- **EditorConfig**: `.editorconfig` (4-space indent, LF line endings)
- **Naming**: PascalCase for types, camelCase for methods/params, `_camelCase` for private fields
- **Namespace**: File-scoped namespaces
- **Imports**: Sorted, system directives first, single group (no separation)

## Important Notes

1. **Functional tests require AppHost**: They use `TestAppHost` project as a bootstrap
2. **Database interceptors**: `AuditableEntityInterceptor` and `DispatchDomainEventsInterceptor` are registered in infrastructure
3. **Pipeline behaviors**: Logging, exception handling, authorization, validation, and performance tracking are all registered in Application DI
4. **Guard validations**: Use `Guard.Against.Null()` for connection string validation
