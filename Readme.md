# Enterprise Time & Attendance Management System (TAMS)

A secure, cloud-ready **Time & Attendance Management System** built with ASP.NET Core 8
(Clean Architecture) and React 19, integrating with **ZKTeco** biometric devices to
capture, process, report, and audit employee attendance.

> **Documentation-first project.** The full design (`docs/01`–`14`) was authored and
> approved before implementation. The code is built phase-by-phase against that design,
> with each phase verified end-to-end.

---

## Status

| Phase | Scope | State |
|---|---|---|
| **P0** | Documentation (BRD → Maintenance Guide) | ✅ Approved |
| **P1** | Foundation: Auth (JWT + refresh), RBAC, Employee, Department, audit, SPA shell | ✅ Complete |
| **P2** | Shift management + Attendance calculation engine | ✅ Complete |
| **P3** | ZKTeco device integration (resilient worker, offline recovery) | ⏳ Next |
| P4–P7 | Leave, Reporting, Hardening, Deployment | ⬜ Planned |

Build: **0 warnings / 0 errors** · Tests: **49 passing** (domain, application, architecture)

---

## Tech stack

**Backend** — ASP.NET Core 8, EF Core, MediatR (CQRS), FluentValidation, Serilog, SQL Server, JWT
**Frontend** — React 19, TypeScript (strict), Tailwind, React Query, React Hook Form, Axios
**Architecture** — Clean Architecture, DDD, Repository pattern, 12-Factor, OWASP Top 10

## Repository layout

```
docs/     # 00–14 approved documentation set (the source of truth)
src/      # TAMS.Domain / Application / Infrastructure / Api / Worker
client/   # React 19 + TypeScript SPA
tests/    # Domain / Application / Architecture test suites
```

## Getting started

Prerequisites: **.NET 8 SDK**, **Node 20+**, **SQL Server** (LocalDB works for dev).

```bash
# Backend (applies migrations + seeds a dev admin on first run)
dotnet run --project src/TAMS.Api

# Frontend (proxies /api to the API)
cd client && npm install && npm run dev
```

Dev login: `admin` / `ChangeMe!123` (force-changed on first login; real secrets come from
the secret store per `docs/06_SECURITY.md`).

Full build/run/test instructions: see [`SOLUTION_README.md`](SOLUTION_README.md).

## Key design guarantees

- **Accuracy** — attendance math lives in a pure, I/O-free `AttendanceCalculator`, exhaustively unit-tested (overnight, DST, grace, OT, missing punches).
- **Auditability** — every attendance-affecting action writes an append-only audit entry **in the same transaction** as the change; raw punches are immutable.
- **Security** — JWT + rotating/revocable refresh tokens, permission-based RBAC (deny-by-default), PBKDF2 hashing, correlation-id tracing.
- **Cloud-ready** — 12-Factor, no cloud lock-in in the core; migration is a re-host, not a rewrite.

## Documentation

Start with [`docs/00_PROJECT_CONTEXT.md`](docs/00_PROJECT_CONTEXT.md), then the numbered set
`01`–`14`. Outstanding stakeholder inputs are tracked in
[`docs/OQ_STAKEHOLDER_QUESTIONNAIRE.md`](docs/OQ_STAKEHOLDER_QUESTIONNAIRE.md).
