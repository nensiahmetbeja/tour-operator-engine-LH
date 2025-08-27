# Data Import Engine

This repository implements a tour operator pricing import and query engine for the Lufthansa .NET Assessment. It features CSV uploads, JWT authentication, PostgreSQL + EF Core, Redis caching, and SignalR real-time progress updates. Structured logging is enabled via the built-in ILogger (Serilog-compatible).

## Tech Stack

- **.NET 9.0**, ASP.NET Core
- **EF Core**, PostgreSQL
- **Redis** (IDistributedCache)
- **SignalR** (real-time progress)
- **CsvHelper** (CSV parsing)
- **Swagger/OpenAPI**
- **Structured logging** (ILogger; works with Serilog sinks)

## Prerequisites

- .NET 9.0 SDK
- Docker Desktop (for PostgreSQL and Redis)
- Optional: a DB client (e.g., DBeaver) for inspection
- A browser for UI testing

## Getting Started

### Clone & Restore

```bash
git clone https://github.com/nensiahmetbeja/tour-operator-engine-LH.git
cd tour-operator-engine-LH
dotnet restore
```

### Running the Services

1. **Start PostgreSQL and Redis** via Docker (or any local instances):
   ```bash
   docker compose up -d
   ```
   *(Ensure docker-compose.yml is present or use your own containers.)*

   **Default connections:**
    - **PostgreSQL**: `Host=localhost;Port=5432;Database=lufthansa_pricing;Username=postgres;Password=secret`
    - **Redis**: `localhost:6379`

3. **Database setup:**
(choose one): Quick start (auto-create on first run) 
OR Migrations (recommended)

   ```bash
   dotnet ef database update -p Lufthansa.Infrastructure -s Lufthansa.Api
   ```

4. **Run the API**:
   ```bash
   cd Lufthansa.Api
   dotnet run
   ```
   By default, the API runs on `http://localhost:5043`.

## Credentials

Pre-seeded demo users:

**Tour Operator:**
- Username: `op1`
- Password: `op123`

**Admin:**
- Username: `admin`
- Password: `admin123`

Tokens are generated via a mock login endpoint:
`POST /api/auth/login`

for example: JSON body { "username": "op1", "password": "op123" } returns access_token used as Bearer in Authorization.

## UI for Testing

A basic test harness is provided at:
**http://localhost:5043/test-upload.html**

Features:
- Upload CSV files with progress reporting via SignalR
- Observe live updates
- Change operator ID, token, API base

## API Endpoints

- **Swagger UI**: http://localhost:5043/swagger
- **Pricing Upload**: `POST /api/touroperators/{tourOperatorId}/pricing-upload` (requires TourOperatorRole)
- **Admin Query**: `GET /api/data/{tourOperatorId}?page=1&pageSize=50` (requires AdminRole)
    - the tourOperatorId in the URL must match the tourOperatorId claim in the token (otherwise Forbid)


## Documentation 
https://github.com/nensiahmetbeja/tour-operator-engine-LH/tree/main/docs

- **Postman Collection**: see `docs/Lufthansa API.postman_collection.json`
- **Architecture Diagram**: see `docs/architecture`

## Design Decisions and Trade-offs

### Technology Stack

- **.NET 9.0**, EF Core, PostgreSQL, Redis, SignalR, CsvHelper
- Swagger/OpenAPI for testing

### Architecture

**Layers:**
- **Domain**: entities and rules
- **Application**: interfaces, DTOs, services
- **Infrastructure**: EF Core, Redis, CSV, persistence
- **API**: minimal endpoints, DI, Swagger, SignalR hub

<img width="3840" height="646" alt="architecture" src="https://github.com/user-attachments/assets/5ac4189a-6aca-4f28-9bec-44d60a1e2a3b" />

**Trade-off**: Clean layering adds complexity but helps maintainability.

### Containerized dependencies (Docker): run Postgres and Redis via docker compose.
- **Why**: one-command reproducible setup across machines; eliminates “works on my machine” issues; clean reset and consistent versions for local dev and CI.
-**Trade-off:** adds Docker Desktop as a dependency and consumes more resources than local installs; startup and image pulls can be slower.
-**Note:** API can be run locally or containerized later; DB/cache isolation is the key win here

### Authentication
**Chosen approach**: JWT Bearer with role-based authorization (e.g., TourOperator, Admin).
- **Why**: stateless, easy to scale and cache, works for both HTTP APIs and SignalR.
- **Trade-off**: managing token issuance/rotation and revocation becomes your responsibility.

**Token issuance**: demo-friendly login that issues short-lived tokens signed with a symmetric key.
- **Why**: fast to set up and easy to run locally.
- **Trade-off**: in production, prefer OIDC (e.g., Azure AD, Auth0, Keycloak) with asymmetric signing and key rotation.

**Claims model**: include role and a tenant identifier claim to enforce multi-tenant boundaries at the API.
- **Why**: simple, server-side checks are straightforward.
- **Trade-off**: coupling to a specific claim shape; keep claims minimal and well-documented.

**Revocation strategy**: short token lifetime, optionally complemented by a JTI blacklist (e.g., Redis) for immediate revoke.
- **Why**: balances simplicity with operational control.
- **Trade-off**: blacklist adds complexity; without it you rely on expiry for revocation.

**SignalR considerations**: use Bearer tokens for the hub connection; the client also passes a connectionId to correlate upload progress.
- **Why**: simplest way to route progress to the correct client.
- **Trade-off**: must validate that the provided connectionId belongs to the authenticated user/session to prevent spoofing; server-side mapping is recommended.
- **Note**: The test page passes connectionId via query string for demo purposes; production should bind connectionId to the authenticated user server-side to prevent spoofing.

**Production hardening** (future work):
- OIDC with an external IdP, PKCE for SPA flows
- Key management and rotation (JWKS), asymmetric signing
- Refresh tokens with rotation and reuse detection
- Scope/permission-based authorization (beyond roles)
- Server-side mapping/validation of SignalR connectionId to user
- Centralized audit logging of auth events

### CSV Import

- CSVHelper parsing, strict validation
- Duplicate handling: skip, overwrite, error
- Bulk insert first, fallback per-row
- **Trade-off**: Per-row slower but reliable.

### Progress Reporting

- SignalR hub, client passes connectionId
- Messages: "Validation started", "50% processed", "Bulk insert completed"
- **Trade-off**: Query string for connectionId less secure, but simpler and matches assessment spec.

### Caching

- Redis with version keys and TTL-based expiration (default 60 seconds)
- Invalidates on upload
- Pagination for queries
- **Trade-off**: Simple TTL-free cache; enough for assessment.

### Seeding

- Static GUIDs and timestamps
- Runtime seeding to avoid EF migration warnings

## Notes for Reviewers

- SignalR notifier is under `Lufthansa.Api/Realtime`
- Redis caching, upload service, and query service are in `Lufthansa.Infrastructure`
- Entity models and interfaces are in `Lufthansa.Domain` and `Lufthansa.Application`
- JWT, CORS, DI configured in `Program.cs`
- Clean Architecture adapted to fit assessment timeline: some helpers live in API layer
