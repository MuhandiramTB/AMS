# TAMS — Implementation (Phase 1: Foundation)

This is the working codebase for the Enterprise Time & Attendance Management System,
built strictly against the approved documentation set in [`docs/`](docs/) (`01`–`14`).

> **Status: Phase 1 (Foundation) complete and verified.** Subsequent phases
> (P2 Attendance core, P3 ZKTeco, P4 Leave, P5 Reporting, P6 Hardening) follow the
> plan in [`docs/09_DEVELOPMENT_PLAN.md`](docs/09_DEVELOPMENT_PLAN.md).

## What Phase 1 delivers (the "walking skeleton")

An end-to-end vertical slice proving every layer and the toolchain integrate:

- **Clean Architecture** solution: Domain → Application → Infrastructure → Api (+ Worker stub)
- **Authentication** (JWT + rotating refresh tokens, PBKDF2 hashing, lockout)
- **Permission-based RBAC** (deny-by-default, capability matrix seeded from SRS §4.1)
- **Employee & Department** management (CRUD, validation, soft delete)
- **Append-only audit trail** via an EF `SaveChanges` interceptor (FR-AUD-001)
- **Global RFC 9457 error handling** + correlation ids end-to-end
- **React 19 SPA**: login, role-filtered shell, employees/departments pages (React Query, RHF, Axios, Tailwind)
- **Architecture tests** mechanically enforcing the Dependency Rule (ADR-004)
- **18 passing tests** (domain, application, architecture)

## Prerequisites

- .NET 8 SDK (pinned via `global.json`)
- Node 20+ / npm
- SQL Server — LocalDB (`MSSQLLocalDB`) works out of the box for dev

## Run the backend (API)

```bash
# from the repo root
dotnet build TAMS.sln
dotnet run --project src/TAMS.Api
```

On first run the API **applies migrations and seeds** roles, permissions, and a
bootstrap admin (dev only). API listens on the configured URL (e.g. `http://localhost:5080`).

Bootstrap admin (dev): `admin` / `ChangeMe!123` — **change on first login**; in
production the password and JWT signing key come from the secret store, never source
(see [`docs/06_SECURITY.md`](docs/06_SECURITY.md) §9).

Health checks: `GET /api/v1/health/live`, `GET /api/v1/health/ready`.
API docs (dev): `/swagger`.

## Run the frontend (SPA)

```bash
cd client
npm install
npm run dev          # http://localhost:5173, proxies /api to the API
```

## Run the tests

```bash
dotnet test TAMS.sln          # backend: domain + application + architecture
cd client && npm run build    # frontend type-check + build
```

## Database migrations

```bash
# add a migration
dotnet ef migrations add <Name> --project src/TAMS.Infrastructure --startup-project src/TAMS.Api --output-dir Persistence/Migrations
# apply
dotnet ef database update --project src/TAMS.Infrastructure --startup-project src/TAMS.Api
```

> Production migrations are applied via a controlled deploy step, **not** app
> startup (see [`docs/11_DEPLOYMENT.md`](docs/11_DEPLOYMENT.md) §6).

## Project layout

```
src/
  TAMS.Domain/          # entities, value objects, invariants — no dependencies
  TAMS.Application/      # CQRS handlers, ports, validators, pipeline behaviours
  TAMS.Infrastructure/   # EF Core, repositories, audit interceptor, security, seeder
  TAMS.Api/             # ASP.NET Core host (composition root, controllers, middleware)
  TAMS.Worker/          # ZKTeco background worker (implemented in Phase 3)
client/                 # React 19 + TS + Tailwind SPA
tests/                  # Domain / Application / Architecture test suites
docs/                   # 00–14 approved documentation set (source of truth)
```

## Notes / open items

- **ZKTeco integration is Phase 3** and will be built behind an `IDeviceGateway`
  port with a realistic simulator (per the plan), pending the device SDK details
  (open question OQ-01, see [`docs/OQ_STAKEHOLDER_QUESTIONNAIRE.md`](docs/OQ_STAKEHOLDER_QUESTIONNAIRE.md)).
- Dev secrets in `appsettings.json` are **placeholders**; real secrets are injected
  from the environment/secret store in Test/Staging/Prod.
