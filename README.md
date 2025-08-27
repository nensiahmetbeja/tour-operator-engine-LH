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
git clone <your-repo-url>
cd tour-operator-engine-LH
dotnet restore
```

### Running the Services

1. **Start PostgreSQL and Redis** via Docker (or any local instances):
   ```bash
   docker compose up -d
   ```
   *(Ensure docker-compose.yml is present or use your own containers.)*

2. **Apply EF Core migrations and seed data**:
   ```bash
   dotnet ef database update -p Lufthansa.Infrastructure -s Lufthansa.Api
   ```

3. **Run the API**:
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

Tokens are generated via a mock login endpoint or seeded token values; see `/test-upload.html` for usage.

## UI for Testing

A basic test harness is provided at:
**http://localhost:5043/test-upload.html**

Features:
- Upload CSV files with progress reporting via SignalR
- Observe live updates
- Change operator ID, token, API base

## API Endpoints

- **Swagger UI**: http://localhost:5043/swagger
- **Pricing Upload**: `POST /api/touroperators/{tourOperatorId}/pricing-upload`
- **Admin Query**: `GET /api/admin/pricing?page=1&pageSize=50`

For details, copy the OpenAPI JSON URL from Swagger UI and import into Postman.

## Documentation

- **Postman Collection**: see `docs/postman`
- **Architecture Diagram**: see `docs/architecture`

## Design Decisions and Trade-offs

### Technology Stack

- **.NET 9.0**, EF Core, PostgreSQL, Redis, SignalR, CsvHelper
- Swagger/OpenAPI for testing
- **Trade-off**: Used .NET 9.0 preview packages; more modern but watch compatibility.

### Architecture

**Layers:**
- **Domain**: entities and rules
- **Application**: interfaces, DTOs, services
- **Infrastructure**: EF Core, Redis, CSV, persistence
- **API**: minimal endpoints, DI, Swagger, SignalR hub

<img width="3840" height="646" alt="architecture" src="https://github.com/user-attachments/assets/5ac4189a-6aca-4f28-9bec-44d60a1e2a3b" />

**Trade-off**: Clean layering adds complexity but helps maintainability.

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

**Browser vs API**: prefer Bearer tokens over cookies for a pure-API model and to simplify SPA/testing.
- **Trade-off**: must store tokens safely in the browser (ideally in memory, use refresh-token rotation if needed); cookies would require CSRF defenses.

**CORS and transport security**: restrict allowed origins in development and use HTTPS.
- **Why**: reduces attack surface and protects tokens in transit.

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

- Redis with version keys
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
