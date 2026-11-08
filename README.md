# Multi-Carrier Shipping API

A .NET 10 Web API that creates shipments through a single endpoint across multiple carriers (UPS and FedEx) and stores each shipment with its tracking number and label. Callers send one carrier-neutral request; carrier adapters translate it to each carrier's own API.

> **Status:** Real UPS and FedEx adapters exist, but they have only been tested against fake HTTP responses, not yet against the carriers' test environments. Each carrier has a mock adapter too. `Carriers:<Carrier>:UseMock` picks which one is registered; check it in `src/Web/appsettings.json` before you run.

## Tech Stack

- .NET 10, ASP.NET Core Minimal APIs
- .NET Aspire for local orchestration and OpenTelemetry
- EF Core with PostgreSQL
- ASP.NET Core Identity with bearer tokens
- MediatR (CQRS), FluentValidation, AutoMapper
- OpenAPI with a Scalar API reference
- NUnit, Shouldly, Moq and Respawn for tests

## Architecture

The solution follows Clean Architecture, with dependencies pointing inward:

```
Web / AppHost  ──>  Infrastructure  ──>  Application  ──>  Domain
```

| Project | Responsibility |
|---------|----------------|
| `src/Domain` | `Shipment` entity, value objects (`Address`, `Package`, `Weight`, `Dimensions`), enums and domain events |
| `src/Application` | Commands, queries, validators and the interfaces the outer layers implement (`ICarrierShipmentAdapter`, `IApplicationDbContext`) |
| `src/Infrastructure` | EF Core persistence, Identity and carrier adapters |
| `src/Web` | HTTP endpoints, authentication and the OpenAPI document |
| `src/AppHost` | Aspire host that runs PostgreSQL and the API together |
| `src/ServiceDefaults` | Shared telemetry, health check and service discovery setup |
| `src/Shared` | Resource names shared by the AppHost and the API |

### Carrier adapters

Every carrier implements `ICarrierShipmentAdapter`, and `ICarrierAdapterFactory` picks the adapter from the request's `carrier` value. For each carrier, the `Carriers:<Carrier>:UseMock` setting decides whether the mock or the real adapter is registered.

The real adapters (`Infrastructure/Carriers/Ups/`, `Infrastructure/Carriers/FedEx/`) work the same way:

- They get an OAuth client-credentials token and cache it until it is about to expire.
- They request ZPL labels and return them as `LabelFormat.Zpl`.
- They map `serviceLevel` to the carrier's service code. Names such as `Ground`, `Next Day Air` or `Priority Overnight` work, and so do raw carrier codes. If `serviceLevel` is empty, UPS uses Ground (`03`) and FedEx uses `FEDEX_GROUND`. An unknown value is a rejected shipment (422).
- A carrier rejection (HTTP 400 or 422) becomes a failed shipment instead of an exception.
- Only the first package's label and tracking number are returned for multi-package shipments.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (version pinned in `global.json`)
- Docker, used by Aspire to run PostgreSQL

### Run

```bash
dotnet run --project src/AppHost
```

The Aspire dashboard opens with the service URLs and logs. The API redirects `/` to the Scalar API reference at `/scalar`.

In Development, the database is migrated and seeded on startup with an administrator account:

- Email: `administrator@localhost`
- Password: `Administrator1!`

### Configuration

Carrier settings live in `src/Web/appsettings.json`:

```json
"Carriers": {
  "Ups":   { "UseMock": false, "ApiUrl": "https://wwwcie.ups.com",            "AccountNumber": "", "ClientId": "", "ClientSecret": "" },
  "FedEx": { "UseMock": true,  "ApiUrl": "https://apis-sandbox.fedex.com", "AccountNumber": "", "ClientId": "", "ClientSecret": "" }
}
```

To use a real carrier, set `UseMock` to `false` and fill in `AccountNumber` (the account that pays for the shipment), `ClientId` and `ClientSecret`. For local development, keep these in user secrets:

```bash
dotnet user-secrets --project src/Web set "Carriers:Ups:AccountNumber" "<value>"
dotnet user-secrets --project src/Web set "Carriers:Ups:ClientId" "<value>"
dotnet user-secrets --project src/Web set "Carriers:Ups:ClientSecret" "<value>"
```

The URLs above are the carriers' test environments. For production, use `https://onlinetools.ups.com` for UPS and `https://apis.fedex.com` for FedEx.

Do not commit carrier credentials. Provide them through user secrets locally, or through Azure Key Vault by setting `AZURE_KEY_VAULT_ENDPOINT`. For now the account and credentials are global per carrier; per-user carrier accounts are planned in [ADR 0001](docs/decisions/0001-carrier-account-credentials.md).

## API

All shipment endpoints require a bearer token.

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/Users/register` | Create a user account |
| `POST` | `/api/Users/login` | Get an access token and refresh token |
| `POST` | `/api/Users/refresh` | Exchange a refresh token for a new access token |
| `POST` | `/api/Shipments` | Create a shipment with the selected carrier |
| `GET` | `/api/Shipments/{id}` | Get a shipment by ID |

### Create a shipment

```http
POST /api/Shipments
Authorization: Bearer <token>
Content-Type: application/json

{
  "carrier": 1,
  "shipFrom": {
    "name": "Acme Warehouse",
    "street1": "123 Main St",
    "city": "Chicago",
    "state": "IL",
    "postalCode": "60601",
    "countryCode": "US",
    "phone": "3125550100"
  },
  "shipTo": {
    "name": "Jane Doe",
    "street1": "456 Oak Ave",
    "city": "Atlanta",
    "state": "GA",
    "postalCode": "30301",
    "countryCode": "US",
    "phone": "4045550100"
  },
  "packages": [
    {
      "weight": { "value": 5.5, "unit": 1 },
      "dimensions": { "length": 12, "width": 8, "height": 6, "unit": 1 },
      "description": "Books"
    }
  ],
  "serviceLevel": "Ground"
}
```

`phone` is optional, but the carriers may reject a shipment without one. Enum values are sent as numbers:

| Field | Values |
|-------|--------|
| `carrier` | `1` = UPS, `2` = FedEx |
| `weight.unit` | `1` = lb, `2` = kg |
| `dimensions.unit` | `1` = in, `2` = cm |
| `labelFormat` (response) | `1` = PDF, `2` = ZPL, `3` = URL |
| `status` (response) | `1` = Pending, `2` = Created, `3` = Failed |

Responses:

- **201 Created**: the carrier accepted the shipment. The body holds the shipment ID, tracking number, label data and carrier reference.
- **400 Bad Request**: the request failed validation.
- **422 Unprocessable Entity**: the carrier rejected the shipment. It is stored with status `Failed` and the body explains why.

Users can only read the shipments they created. Administrators can read all shipments.

## Development

### Build

```bash
dotnet build
```

Warnings are treated as errors. Package versions are managed centrally in `Directory.Packages.props`.

### Database migrations

```bash
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Web --output-dir Data/Migrations
```

### Tests

Run the full suite:

```bash
dotnet test
```

| Project | Type | Needs Docker |
|---------|------|--------------|
| `tests/Domain.UnitTests` | Domain unit tests | No |
| `tests/Application.UnitTests` | Application unit tests | No |
| `tests/Infrastructure.IntegrationTests` | Persistence against PostgreSQL | Yes |
| `tests/Application.FunctionalTests` | End-to-end use cases against PostgreSQL | Yes |

The integration and functional tests start PostgreSQL through `tests/TestAppHost`.

### CI

GitHub Actions (`.github/workflows/ci.yml`) restores, builds in Release and runs every test on each push.
