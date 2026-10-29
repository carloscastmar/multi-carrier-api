# Project: Multi-Carrier Shipping API

A .NET API that creates shipments through one interface over multiple carriers (UPS, FedEx) and stores them with tracking and label data. Built with Clean Architecture and run with .NET Aspire.

## Tech Stack (WHAT)
- **Runtime:** .NET 10 (SDK pinned in `global.json`), Minimal APIs, nullable enabled
- **Orchestration:** .NET Aspire (`src/AppHost`), ServiceDefaults for OpenTelemetry, health checks and service discovery
- **Data:** EF Core + PostgreSQL (Npgsql), ASP.NET Core Identity with bearer tokens
- **Patterns:** Clean Architecture, CQRS with MediatR, FluentValidation, AutoMapper, Ardalis.GuardClauses
- **API docs:** OpenAPI + Scalar (`/scalar`)
- **Tests:** NUnit, Shouldly, Moq, Respawn; Aspire `TestAppHost` provides PostgreSQL for integration and functional tests

## Architecture (WHY)
Dependencies only flow inward:
`Web / AppHost` ──> `Infrastructure` ──> `Application` ──> `Domain`

1. **Domain:** Entities, Value Objects, Enums, Domain Events and Constants. No EF Core and no web references (only `MediatR.Contracts` for events). Entities protect their own state: private setters, a static `Create(...)` factory and intent methods (`MarkCreated`, `MarkFailed`), never public setters.
2. **Application:** Use cases organised by feature (`Shipments/Commands/<UseCase>/`, `Shipments/Queries/<UseCase>/`). Each use case is a command/query record, its handler and its `AbstractValidator`. Interfaces live in `Common/Interfaces` (`IApplicationDbContext`, `ICarrierShipmentAdapter`, `ICarrierAdapterFactory`). Cross-cutting concerns go in pipeline behaviours (`Common/Behaviours`), not in handlers.
3. **Infrastructure:** Implements the Application interfaces: `ApplicationDbContext`, entity configurations, interceptors (auditing, domain-event dispatch), Identity and carrier adapters.
4. **Web:** Thin endpoint groups (`IEndpointGroup` in `Endpoints/`) that only call `ISender.Send(...)` and turn the result into `TypedResults`. No business logic and no `DbContext` access in endpoints.
5. **AppHost / ServiceDefaults / Shared:** Wiring only. Resource names are constants in `src/Shared/Services.cs`; never hard-code them.

## Carrier Integration Rules
- All carriers sit behind `ICarrierShipmentAdapter`, chosen by `CarrierType` through `ICarrierAdapterFactory`. Handlers never refer to a specific carrier class.
- Each carrier has a real adapter (`Infrastructure/Carriers/<Carrier>/`) and a mock (`Infrastructure/Carriers/Mock<Carrier>Adapter.cs`). `Carriers:<Carrier>:UseMock` in configuration chooses which one is registered in `DependencyInjection.AddCarrierAdapters`.
- Adding a carrier means: a new `CarrierType` value, a real adapter and a mock, an options entry in `CarriersOptions` and `appsettings.json`, registration in `AddCarrierAdapters`, plus tests.
- Carrier-specific request/response shapes stay inside the adapter. Map them to `ShipmentRequest` / `CarrierShipmentResponse`; never let them reach Application or Domain.
- An expected carrier rejection returns `CarrierShipmentResponse.Failure(...)`; it does not throw. The handler stores the shipment as `Failed` and the endpoint returns 422.
- Never commit carrier credentials. `ClientId` / `ClientSecret` stay empty in `appsettings.json` and come from user secrets or Key Vault.

## Coding Principles & Guardrails
- **No over-engineering:** Build exactly what was asked. Don't add abstractions, configuration, docstrings or error handling nobody requested.
- **Surgical changes:** Don't refactor, reformat or "improve" nearby code. Match the existing style (`.editorconfig`, `_camelCase` fields, constructor injection, file-scoped namespaces). Mention unrelated problems you spot instead of fixing them.
- **No silent assumptions:** If an entity property, route, status transition or carrier behaviour is unclear, ask before writing code.
- **Fail fast:** Validate incoming requests with FluentValidation in Application. Put domain invariants in Domain factories and methods. Don't validate raw input deep in Infrastructure.
- **Single source for field lengths:** Column limits live in `Domain/Constants/ShipmentFieldLengths.cs`, used by both the validators and the EF configurations. Change them there only.
- **Async first:** All I/O is async and passes the `CancellationToken` all the way through.
- **Data isolation:** Shipment reads are limited to the user who created them (`CreatedBy`), except for `Administrator`. Every new query must keep this rule.
- **Warnings are errors** (`TreatWarningsAsErrors`). Fix the warning; don't suppress it without asking.
- **Packages:** Versions are managed centrally in `Directory.Packages.props`. Add `<PackageVersion>` there and a version-less `<PackageReference>` in the `.csproj`.

## Core Commands (HOW)

### Build & Run
- Build: `dotnet build`
- Run the full Aspire stack (needs Docker for PostgreSQL): `dotnet run --project src/AppHost`
- In Development, the database is migrated and seeded on startup (`InitialiseDatabaseAsync`).

### EF Core Migrations (from repo root)
- Add: `dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Web --output-dir Data/Migrations`
- Never edit an existing migration that has been pushed; add a new one instead.

### Testing
- All tests (as CI runs them): `dotnet test`
- Fast, no Docker: `dotnet test tests/Domain.UnitTests` and `dotnet test tests/Application.UnitTests`
- `Infrastructure.IntegrationTests` and `Application.FunctionalTests` start PostgreSQL through `TestAppHost` and need Docker.
- Functional tests inherit from `TestBase`, which resets the database before each test.

## Workflow
1. State a short plan with a check for each step (e.g. "add validator → test for the invalid input fails, then passes").
2. Bug fix: write a failing test first. New feature: add tests at the right level (Domain unit, Application unit, functional).
3. Before committing, run `dotnet build` and the relevant tests. CI (`.github/workflows/ci.yml`) builds in Release and runs every test on every push.
4. Commit messages use Conventional Commits, matching the history: `feat(scope):`, `fix(scope):`, `chore:`, `ci:`.
