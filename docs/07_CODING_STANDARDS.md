# 07 — Coding Standards & Engineering Guidelines

## Enterprise Time & Attendance Management System

| Field | Value |
|---|---|
| **Document Title** | Coding Standards & Engineering Guidelines |
| **Project** | Enterprise Time & Attendance Management System (TAMS) |
| **Document ID** | TAMS-STD-007 |
| **Version** | 1.0 (Draft for Approval) |
| **Status** | Awaiting Approval |
| **Author** | Principal Software Architect (AI) |
| **Owner** | Development Lead / Solution Architect |
| **Date** | 2026-07-09 |
| **Classification** | Internal — Confidential |
| **Standards** | **Microsoft .NET / C# Coding Conventions & Framework Design Guidelines**, **SOLID/DRY/KISS/YAGNI**, **Clean Code**, **Airbnb-style TypeScript/React conventions**, **Conventional Commits**, **EditorConfig** |
| **Predecessor Docs** | `01`–`06` (all approved) |
| **Successor Docs** | `08_UI_UX.md`, `09_DEVELOPMENT_PLAN.md`, `10_TESTING_STRATEGY.md` |

> **Scope of this document.** This defines **how code is written** for TAMS — naming, structure, style, patterns, error handling, logging, validation, async, testing conventions, Git workflow, and quality gates — for both the **ASP.NET Core 8 backend** and the **React 19 / TypeScript frontend**. It makes the architecture (`03`), API (`05`) and security (`06`) decisions *enforceable at the code level*.
>
> **Boundary with other docs.** This is the *rulebook*; it does not restate architecture, schema, or endpoints. Where a rule enforces another doc's decision, it references it. Test *strategy/coverage targets* are owned by `10_TESTING_STRATEGY.md`; this doc defines test *code conventions*. CI/CD *pipeline mechanics* are in `11_DEPLOYMENT.md`; this doc defines the *quality gates* the pipeline must enforce.
>
> **Enforcement philosophy.** A standard that relies on memory is a suggestion. Wherever possible these rules are enforced by tooling (analyzers, linters, formatters, EditorConfig, architecture tests, CI gates) so violations **fail the build**, not the review.

---

## Document Control

### Revision History

| Version | Date | Author | Description |
|---|---|---|---|
| 1.0 | 2026-07-09 | AI Architect | First complete coding standards derived from approved Security design v1.0 |

### Approval Sign-off

| Role | Name | Signature | Date |
|---|---|---|---|
| Development Lead | _TBD_ | | |
| Solution Architect | _TBD_ | | |
| Frontend Lead | _TBD_ | | |
| QA Lead | _TBD_ | | |

---

## Table of Contents

1. [Guiding Principles](#1-guiding-principles)
2. [Cross-Cutting Conventions](#2-cross-cutting-conventions)
3. [Backend — C# / .NET Standards](#3-backend--c--net-standards)
4. [Backend — Layer-Specific Rules (Clean Architecture)](#4-backend--layer-specific-rules-clean-architecture)
5. [Backend — Error Handling, Logging, Validation](#5-backend--error-handling-logging-validation)
6. [Backend — Async & Performance](#6-backend--async--performance)
7. [Backend — Security Coding Rules](#7-backend--security-coding-rules)
8. [Frontend — TypeScript / React Standards](#8-frontend--typescript--react-standards)
9. [Frontend — State, Data & Forms](#9-frontend--state-data--forms)
10. [Testing Conventions](#10-testing-conventions)
11. [Git Workflow & Commit Conventions](#11-git-workflow--commit-conventions)
12. [Tooling & Automated Enforcement](#12-tooling--automated-enforcement)
13. [Code Review Standards](#13-code-review-standards)
14. [Documentation & Comments](#14-documentation--comments)
15. [Anti-Patterns (Prohibited)](#15-anti-patterns-prohibited)
16. [Traceability (Principles → Rules)](#16-traceability-principles--rules)
17. [Glossary](#17-glossary)
18. [Documentation Review Checklist](#18-documentation-review-checklist)

---

# 1. Guiding Principles

| ID | Principle | Practical meaning in code |
|---|---|---|
| CP-01 | **SOLID** | Small, single-purpose classes; depend on abstractions |
| CP-02 | **DRY** | No copy-paste; extract shared logic (but not prematurely) |
| CP-03 | **KISS** | Simplest solution that works; avoid clever code |
| CP-04 | **YAGNI** | Build what requirements need now, not speculative flexibility |
| CP-05 | **Clean Code** | Intention-revealing names; short functions; low nesting |
| CP-06 | **Composition over inheritance** | Prefer interfaces + DI over deep hierarchies |
| CP-07 | **Fail fast, fail safe** | Validate early; on error deny & log, never swallow |
| CP-08 | **Explicit over implicit** | No hidden side effects; clear contracts |
| CP-09 | **Consistency** | The codebase reads as if written by one author |
| CP-10 | **Testability first** | If it's hard to test, the design is wrong |

**Decision — "consistency over personal preference" (CP-09).** Where two styles are both reasonable, the team picks one and everyone follows it. A uniform codebase lowers cognitive load, speeds review, and makes onboarding trivial — the value of *a* convention almost always exceeds the value of the "best" convention. This is why nearly every rule below is enforced by a formatter/analyzer, removing style debates from PRs.

---

# 2. Cross-Cutting Conventions

| Aspect | Rule |
|---|---|
| Encoding / line endings | UTF-8; LF; final newline; via `.editorconfig` |
| Indentation | Spaces (C#: 4, TS: 2) via `.editorconfig` |
| Max line length | ~120 chars (soft), enforced by formatter |
| Language | Code, comments, identifiers in **English** |
| Time | **UTC** in code/storage; localise only at UI edge (`04 DP-07`, `05 §2`) |
| Booleans | Positive names (`isActive`, not `notInactive`) |
| Magic values | No magic numbers/strings — use constants/enums/config |
| TODO/FIXME | Must include owner + ticket ref; no orphan TODOs in main |
| Secrets | **Never** in source (enforced by secret scanning, `06 §9`) |

**Decision — a single committed `.editorconfig` is the root of truth for style.** Both C# and TS/JS honour `.editorconfig`, so indentation, line endings and basic style are enforced identically in every IDE and in CI. This eliminates "works on my machine" formatting churn and keeps diffs meaningful.

---

# 3. Backend — C# / .NET Standards

## 3.1 Naming (Microsoft Framework Design Guidelines)

| Element | Convention | Example |
|---|---|---|
| Namespace | PascalCase, `TAMS.<Layer>.<Feature>` | `TAMS.Application.Attendance` |
| Class / record / struct | PascalCase, noun | `AttendanceRecord`, `CreateEmployeeCommand` |
| Interface | PascalCase, `I` prefix | `IEmployeeRepository`, `IDeviceGateway` |
| Method | PascalCase, verb | `CalculateWorkedMinutes()` |
| Async method | PascalCase + **`Async` suffix** | `GetByIdAsync()` |
| Public property | PascalCase | `WorkedMinutes` |
| Private field | `_camelCase` | `_repository` |
| Local var / param | camelCase | `employeeId` |
| Constant | PascalCase | `MaxPageSize` |
| Enum | PascalCase singular; members PascalCase | `AttendanceStatus.Processed` |
| Type parameter | `T` prefix | `TEntity` |
| Boolean | `Is/Has/Can` prefix | `IsFinalized` |

## 3.2 Language & style rules

| Rule | Standard |
|---|---|
| `var` usage | Use when type is obvious from RHS; explicit type otherwise |
| Nullable reference types | **Enabled** (`<Nullable>enable</Nullable>`) — no implicit nulls |
| Immutability | Prefer `record`/`readonly`/init-only for DTOs & value objects |
| Expression-bodied members | For trivial one-liners only |
| Pattern matching | Prefer over long `if/else` chains |
| `using` | File-scoped namespaces; `global using` for common imports; `using` declarations for `IDisposable` |
| String comparison | Explicit `StringComparison` (`Ordinal`/`OrdinalIgnoreCase`) |
| LINQ | Readable method syntax; avoid nested/opaque queries |
| Guard clauses | Validate arguments early; `ArgumentNullException.ThrowIfNull` |
| Access modifiers | Always explicit; smallest that works (`internal`/`private` by default) |

**Decision — nullable reference types ON, project-wide, treated as errors.** Enabling NRTs turns an entire class of `NullReferenceException` bugs into compile-time errors and makes intent explicit (`string?` vs `string`). Combined with guard clauses, this is the cheapest, highest-leverage correctness gain in C# — so it is mandatory, not optional, and warnings are promoted to errors.

## 3.3 File & project organisation

```text
TAMS.<Layer>/
  <Feature>/            # organise by feature/vertical slice within a layer
    Commands/
    Queries/
    Dtos/
    Validators/
    ...
```

**Decision — organise by feature (vertical slice) within each layer, not by technical type.** Grouping all commands for *Attendance* together (rather than one giant `Commands` folder across all features) keeps related code cohesive, makes a feature easy to find and reason about, and aligns folders with the bounded contexts (`03 §8`). One class per file; file name = type name.

---

# 4. Backend — Layer-Specific Rules (Clean Architecture)

Enforces the Dependency Rule (`03 §7`, ADR-004). **An architecture-test project fails the build if any rule below is violated** (§12).

| Layer | MUST | MUST NOT |
|---|---|---|
| **Domain** | Contain entities, value objects, domain services, domain events; be pure C# | Reference EF Core, ASP.NET, MediatR, AutoMapper, or any infrastructure package |
| **Application** | Define use cases (CQRS commands/queries via MediatR), **ports (interfaces)**, validators, mapping profiles; depend only on Domain | Reference concrete infrastructure (EF, HTTP, ZKTeco SDK) |
| **Infrastructure** | Implement ports (EF repositories, `IDeviceGateway`, JWT, Serilog sinks, export writers) | Contain business rules; be referenced by Domain/Application |
| **Presentation (Api/Worker)** | Host, wire DI (composition root), map HTTP↔commands; thin | Contain business logic; access DbContext directly |

## 4.1 CQRS / MediatR conventions

| Rule | Standard |
|---|---|
| One command/query per use case | `CreateEmployeeCommand` + `CreateEmployeeCommandHandler` |
| Handlers are thin orchestrators | Delegate rules to domain; no fat handlers |
| Commands mutate, queries read | Queries never change state (CQS) |
| Cross-cutting via pipeline behaviours | Validation → logging → transaction → audit (ADR-007) |
| No business logic in controllers | Controllers dispatch to MediatR and return results |

**Decision — controllers are thin; the handler is the unit of work.** A controller's only jobs are: bind/authorize the request, dispatch a command/query, and translate the result to HTTP. All logic lives in handlers and the domain. This keeps the HTTP layer swappable (a Worker can invoke the same handler, `03 ADR-003`), makes logic testable without HTTP, and prevents the "fat controller" anti-pattern.

## 4.2 Repository & persistence rules

| Rule | Standard |
|---|---|
| Repositories return domain types / projections | Not raw `IQueryable` leaking EF across layers |
| DbContext lives in Infrastructure only | Application depends on `IRepository`/`IUnitOfWork` |
| No lazy loading | Explicit `Include`/projection to avoid N+1 (§6) |
| Read queries | Projected to DTOs with `Select` (no over-fetch) |
| Writes | Through aggregates + `IUnitOfWork`; one transaction per command |

---

# 5. Backend — Error Handling, Logging, Validation

> These realise the non-negotiable mandates from `00_PROJECT_CONTEXT.md` ("never skip validation/logging/exception handling") and `06`.

## 5.1 Error handling

| Rule | Standard |
|---|---|
| Global exception middleware | All unhandled exceptions → RFC 9457 problem details (`05 §6`); no stack traces to client |
| Domain errors | Use typed domain/validation exceptions or result objects — not generic `Exception` |
| Never swallow | No empty `catch {}`; catch only what you can handle, log the rest |
| Fail fast | Guard clauses; throw on invalid state early |
| Expected vs exceptional | Use result/validation for expected failures; exceptions for exceptional cases |
| No control flow via exceptions | Exceptions are not `if` statements |

**Decision — expected failures are results, unexpected failures are exceptions.** Business-rule rejections (leave over balance, duplicate code) are *expected* and returned as validation/result outcomes → mapped to `400/409/422`. Only truly *exceptional* conditions throw. This keeps the happy path fast, stops exceptions being used as expensive control flow, and produces the precise status-code discipline the API contract (`05 §5`) promises.

## 5.2 Logging (Serilog)

| Rule | Standard |
|---|---|
| Structured logging | Named properties, not string interpolation into the message |
| Correlation id | Every log enriched with correlation id (`05`, `06 §11`) |
| Levels | `Error` (failures), `Warning` (recoverable/anomalies), `Information` (key events), `Debug` (dev) |
| No secrets/PII | Serilog destructuring masks sensitive data (`06 §11`) |
| Log at boundaries | Requests, external calls (DB/device), and handled errors — not every line |
| No `Console.WriteLine` | Serilog only |

**Decision — structured logging with masking is mandatory, `Console.WriteLine` is banned.** Structured properties make logs queryable (find all failures for one correlation id/employee) and the masking policy prevents logs becoming a PII breach (`06` A09/A02). Free-text `Console` logging is unqueryable and unscrubbed, so it is prohibited and caught in review.

## 5.3 Validation

| Rule | Standard |
|---|---|
| Every command validated | FluentValidation validator per command, run as pipeline behaviour |
| Server-side authoritative | Client validation is UX only (`06 §7`) |
| Validate at boundary | Reject invalid input before it reaches domain logic |
| Domain invariants | Enforced in the aggregate too (defense in depth, `04 §9`) |

---

# 6. Backend — Async & Performance

| Rule | Standard |
|---|---|
| Async all the way | `async`/`await` for all I/O (DB, device, files); no blocking |
| No `.Result`/`.Wait()`/`async void` | Deadlock/exception hazards — banned (analyzer-enforced) |
| `CancellationToken` | Flowed through handlers, repositories, HTTP endpoints |
| `ConfigureAwait` | Per library/host guidance; consistent |
| Avoid N+1 | Projection/`Include`; verified in review & tests |
| Pagination | Enforced on collections (`05 §7`); never unbounded queries |
| Bulk ops | Batch where appropriate (e.g. punch ingestion) |
| Caching | Only where measured; invalidation considered (YAGNI until needed) |

**Decision — `async void`, `.Result`, and `.Wait()` are banned outright.** These are the top causes of thread-pool starvation and deadlocks in ASP.NET Core. Making them analyzer errors (not review comments) eliminates an entire category of production incidents before code merges. Cancellation tokens flow everywhere so slow/aborted requests release resources promptly — directly supporting the performance NFRs (`02 §6.1`).

---

# 7. Backend — Security Coding Rules

> Code-level enforcement of `06`.

| Rule | Standard | Trace |
|---|---|---|
| Parameterised data access | EF Core / parameters only; **no string-concatenated SQL** | A03 |
| No client trust for authz | Re-check permission + derive scope server-side | `06 §5`, SP-08 |
| DTOs at boundary | Never bind/return EF entities; no over-posting | `05 §1`, A01 |
| Secrets from config | Injected via options/secret store; never hard-coded | `06 §9` |
| Output hygiene | No internal detail/PII in responses, errors, logs | `06 §11` |
| Crypto | Use platform crypto libraries; never roll your own | A02 |
| AuthZ attributes/policies | Every non-public endpoint decorated with required permission policy | `05 §10` |
| Input allow-listing | Validate types/ranges/formats; reject unknown fields | `06 §7` |

**Decision — security rules are code rules, backed by tests.** Each rule here has a corresponding automated check (analyzer for raw-SQL/secrets, auth/authz integration tests for 401/403/scope per `06 §15`). Security that lives only in a document decays; security expressed as failing tests stays enforced as the code evolves.

---

# 8. Frontend — TypeScript / React Standards

## 8.1 Naming & structure

| Element | Convention | Example |
|---|---|---|
| Component file/name | PascalCase | `EmployeeList.tsx` |
| Hook | `useCamelCase` | `useEmployees` |
| Variable/function | camelCase | `fetchEmployees` |
| Type/Interface | PascalCase | `Employee`, `EmployeeDto` |
| Constant | UPPER_SNAKE or PascalCase | `MAX_PAGE_SIZE` |
| Folder | feature-based | `features/attendance/` |
| Boolean prop | `is/has/can` | `isLoading` |

## 8.2 TypeScript rules

| Rule | Standard |
|---|---|
| `strict` mode | **Enabled** (`strict: true`); no implicit `any` |
| `any` | Prohibited except justified escape hatches (lint-flagged) |
| Types from API | Generated/typed DTOs matching OpenAPI (`05 §13`) |
| Immutability | Prefer `const`, readonly types; avoid mutation |
| Discriminated unions | For state/result modelling |
| Null handling | Explicit `undefined`/`null` handling; optional chaining |

## 8.3 React rules

| Rule | Standard |
|---|---|
| Function components + hooks only | No class components |
| Component size | Small, single-responsibility; extract when growing |
| Presentational vs container | Separate data-fetching from presentation |
| Keys | Stable, unique keys in lists (never index when reorderable) |
| Side effects | In `useEffect`/query hooks, with correct deps |
| Accessibility | Semantic HTML, ARIA where needed (WCAG AA, `08`) |
| Styling | Tailwind utility classes; shared patterns extracted to components |
| No inline business logic | Logic in hooks/services, not JSX |

**Decision — `strict` TypeScript with `any` prohibited, and API types generated from OpenAPI.** `strict` + banning `any` gives the frontend the same compile-time safety the backend gets from NRTs. Generating client types from the OpenAPI contract (`05 §13`) means a backend contract change **breaks the frontend build immediately** rather than at runtime in production — the type system enforces client/server agreement, closing the most common integration-bug gap (CP-10).

---

# 9. Frontend — State, Data & Forms

| Concern | Standard | Trace |
|---|---|---|
| **Server state** | **React Query** — caching, retries, invalidation; no server data in Redux/global | mandated stack |
| **Client/UI state** | Local component state or lightweight store; keep minimal | KISS |
| **Forms** | **React Hook Form** + schema validation; mirror server rules (UX only) | `06 §7` |
| **HTTP** | Central **Axios** instance: base URL, JWT header, correlation id, 401→refresh, error normalisation | `05`, `06 §6` |
| **Auth token** | Access token in memory; refresh via HttpOnly cookie flow | `06 §6` |
| **Errors** | Parse RFC 9457 problem details centrally; consistent user messaging | `05 §6` |
| **Loading/empty/error** | Every data view handles all three states | `08` |

**Decision — React Query owns server state; global stores never cache server data.** A dominant frontend anti-pattern is duplicating server data into a global store and hand-writing caching/invalidation. React Query already solves caching, retries, staleness and refetch correctly. Keeping server state in React Query and only *genuine* UI state elsewhere removes a huge class of stale-data bugs and boilerplate (DRY/KISS). The single Axios instance centralises auth, correlation ids and error normalisation so no component reinvents them.

---

# 10. Testing Conventions

> Coverage targets & overall strategy live in `10_TESTING_STRATEGY.md`; here are the **code conventions**.

| Aspect | Standard |
|---|---|
| Test naming | `MethodOrScenario_Condition_ExpectedResult` |
| Structure | **Arrange–Act–Assert** (AAA), one logical assertion focus |
| Domain tests | Pure, fast, no I/O (enabled by pure domain, `03 ADR-006`) |
| Application tests | Handlers with mocked ports |
| Integration tests | Real EF (test DB), API via test host, device via test double |
| Frontend tests | Component/hook tests; user-centric queries (Testing Library) |
| Test data | Builders/factories; no shared mutable fixtures |
| No logic in tests | Tests are straight-line and obvious |
| Determinism | No reliance on real time/random — inject `IClock` (`03 §7`) |
| Isolation | Each test independent; no ordering dependence |

**Decision — inject an `IClock`; never call `DateTime.UtcNow` directly in logic.** Time-dependent logic (shift resolution, overtime, lockout windows) is untestable and flaky if it reads the system clock directly. Injecting `IClock` makes every time-sensitive calculation deterministically testable — which matters enormously for the dispute-prone attendance math (`03 ADR-006`, accuracy G-01).

---

# 11. Git Workflow & Commit Conventions

| Aspect | Standard |
|---|---|
| Branching | Short-lived feature branches off `main`/`develop`; trunk-friendly |
| Branch naming | `feature/…`, `fix/…`, `chore/…` + short desc/ticket |
| Commits | **Conventional Commits**: `type(scope): summary` (`feat`, `fix`, `refactor`, `test`, `docs`, `chore`) |
| Commit size | Small, focused, buildable; no "misc" mega-commits |
| PRs | Required for merge; linked to requirement/ticket; description of what & why |
| PR checks | CI must pass (build, tests, lint, analyzers, security scans) before merge |
| Reviews | ≥1 approval (2 for security-sensitive); no self-merge of unreviewed code |
| No secrets/large binaries | Enforced by scanning + `.gitignore` |
| History | No force-push to shared branches; rebase locally for clean history |

**Decision — Conventional Commits + mandatory green CI before merge.** Structured commit messages enable automated changelogs/versioning and make history readable. Requiring **all** CI gates (build, tests, lint, analyzers, SAST/secret/dependency scans per `06 §15`) to pass *before* merge means `main` is always releasable and quality is enforced by the pipeline, not by hoping reviewers catch everything.

---

# 12. Tooling & Automated Enforcement

| Tool | Enforces | Gate |
|---|---|---|
| **.editorconfig** | Formatting, style, naming | IDE + CI (`dotnet format --verify`) |
| **Roslyn analyzers** (+ `.NET analyzers`, nullable-as-error) | C# correctness/style, async misuse, security | Build (warnings→errors) |
| **StyleCop / analyzer ruleset** | C# style consistency | Build |
| **ESLint + Prettier** | TS/React lint & format | CI + pre-commit |
| **TypeScript `strict`** | Type safety | Build |
| **Architecture tests** (e.g. NetArchTest) | Dependency Rule, layer boundaries (`03`, §4) | Test run |
| **Unit/Integration tests** | Behaviour | CI |
| **SAST / dependency / secret scanning** | Security (`06 §15`) | CI + commit |
| **OpenAPI diff check** | API contract stability (`05 §13`) | CI |
| **Pre-commit hooks** | Format, lint, secret scan locally | Local |

**Decision — the build fails on violations; nothing depends on human vigilance.** Every rule that *can* be machine-checked *is*, and warnings are promoted to build errors. Reviewers then spend their attention on design, correctness and edge cases — the things tools *can't* judge — instead of style nits. This is the operationalisation of the "enforcement philosophy" stated at the top.

---

# 13. Code Review Standards

| Reviewers check | Not |
|---|---|
| Correctness & edge cases | Formatting (tool's job) |
| Adherence to architecture/layer rules | Personal style preference |
| Security implications (`06`) | Bikeshedding |
| Test coverage of new behaviour | |
| Readability & naming | |
| No new anti-patterns (§15) | |

| Rule | Standard |
|---|---|
| Every change reviewed | No unreviewed merges to shared branches |
| Constructive & specific | Comments explain *why*; suggest, don't demand style |
| Author responds to all comments | Resolve or discuss before merge |
| Security-sensitive changes | Second reviewer incl. security awareness |
| Reviewer SLA | Timely review to avoid blocking (team-agreed) |

**Decision — review is for judgment, tools are for rules.** Because formatting/style/simple correctness are automated (§12), reviewers focus exclusively on what humans do better: is this the right design? does it handle the edge cases? is it secure? is it tested? This makes reviews faster, less contentious, and far more valuable.

---

# 14. Documentation & Comments

| Rule | Standard |
|---|---|
| Self-documenting code first | Good names > comments |
| Comment the *why*, not the *what* | Explain intent/trade-offs, not obvious mechanics |
| XML doc comments | On public APIs/contracts (aids IntelliSense + OpenAPI) |
| No dead/commented-out code | Delete it; Git remembers |
| ADRs | Significant decisions recorded (`03 §12` pattern) |
| README per project | How to build/run/test that module |
| Keep docs in sync | Update docs with the code that invalidates them |

**Decision — comments explain *why*; names explain *what*.** A comment restating the code (`// increment i`) is noise that rots. Comments earn their place only when they capture intent, a non-obvious constraint, or a trade-off the code can't express. This keeps the signal-to-noise high and comments trustworthy.

---

# 15. Anti-Patterns (Prohibited)

| # | Anti-pattern | Why prohibited | Enforced by |
|---|---|---|---|
| AP-01 | Business logic in controllers/UI | Untestable, unlayered | Review, arch tests |
| AP-02 | Anemic handlers doing domain logic | Violates DDD; scatters rules | Review |
| AP-03 | `async void`, `.Result`, `.Wait()` | Deadlocks/starvation | Analyzer |
| AP-04 | Empty `catch {}` / swallowing errors | Hides failures | Analyzer/review |
| AP-05 | String-concatenated SQL | Injection (A03) | Analyzer/review |
| AP-06 | Returning EF entities from API | Over-posting/PII leak | Review |
| AP-07 | `any` in TypeScript | Defeats type safety | ESLint |
| AP-08 | Server data cached in global store | Stale-data bugs | Review |
| AP-09 | Magic numbers/strings | Unclear, unmaintainable | Review |
| AP-10 | Secrets in source/logs | Breach risk | Secret scan |
| AP-11 | Skipping validation/logging | Violates mandates | Pipeline design + review |
| AP-12 | God classes / 500-line methods | Unreadable, untestable | Analyzer/review |
| AP-13 | Premature abstraction/over-engineering | Violates YAGNI/KISS | Review |
| AP-14 | Mutating raw punches/audit | Breaks integrity (`04`,`06`) | No endpoint + DB grants |

**Decision — publish the banned list explicitly.** Naming anti-patterns removes ambiguity: a reviewer can point to "AP-05" instead of arguing. Most are tool-enforced; the rest are review-enforced. The list also encodes the project's hard mandates (no skipped validation/logging, no fact/audit mutation) so they can never be "forgotten."

---

# 16. Traceability (Principles → Rules)

| Source mandate | Enforced by rule(s) |
|---|---|
| Clean Architecture / Dependency Rule (`03`) | §4 layer rules + architecture tests (§12) |
| CQRS/MediatR + pipeline behaviours (`03` ADR-007) | §4.1, §5 |
| Pure domain / testability (`03` ADR-006) | §4, §10 (IClock, no-I/O domain tests) |
| API contract (`05`) | §7 (DTOs, authz), §8.2 (generated types), OpenAPI diff (§12) |
| Security (`06`) | §7 security rules, §5.2 logging masking, §12 scans, AP-05/06/10/14 |
| "Never skip validation/logging/exception handling" (`00`) | §5, AP-04/AP-11, pipeline design |
| SOLID/DRY/KISS/YAGNI (`00`) | §1 principles, AP-12/13 |
| Async best practices (`00`) | §6, AP-03 |
| Testability (`02` NFR-20) | §10, §12 architecture/unit tests |

---

# 17. Glossary

Inherits prior docs. Standards-specific additions:

| Term | Definition |
|---|---|
| **NRT** | Nullable Reference Types (C# compile-time null safety). |
| **AAA** | Arrange–Act–Assert test structure. |
| **Analyzer** | Compile-time code rule (Roslyn/ESLint) that can fail the build. |
| **Architecture test** | Automated test asserting layer/dependency rules. |
| **Conventional Commits** | Structured commit message format. |
| **Vertical slice** | Feature-based code organisation across a layer. |
| **IClock** | Injected time abstraction for deterministic tests. |
| **Quality gate** | A CI check that must pass before merge/release. |
| **Over-posting / mass assignment** | Binding untrusted input to more fields than intended. |

---

# 18. Documentation Review Checklist

**Reviewer instructions:** mark ✅ Pass / ⚠️ Needs change / ❌ Fail. Approved when all **Mandatory** items pass.

### 18.1 Completeness

| # | Check | Mandatory | Status |
|---|---|---|---|
| C-01 | Guiding principles stated | ✔ | ☐ |
| C-02 | Cross-cutting conventions defined | ✔ | ☐ |
| C-03 | C#/.NET naming & style rules defined | ✔ | ☐ |
| C-04 | Layer-specific (Clean Architecture) rules defined | ✔ | ☐ |
| C-05 | Error/logging/validation rules defined | ✔ | ☐ |
| C-06 | Async & performance rules defined | ✔ | ☐ |
| C-07 | Backend security coding rules defined | ✔ | ☐ |
| C-08 | TypeScript/React standards defined | ✔ | ☐ |
| C-09 | State/data/forms standards defined | ✔ | ☐ |
| C-10 | Testing conventions defined | ✔ | ☐ |
| C-11 | Git workflow & commit conventions defined | ✔ | ☐ |
| C-12 | Tooling/automated enforcement defined | ✔ | ☐ |
| C-13 | Code review standards defined | ✔ | ☐ |
| C-14 | Documentation/comment rules defined | ✔ | ☐ |
| C-15 | Anti-patterns listed | ✔ | ☐ |

### 18.2 Quality & Soundness

| # | Check | Mandatory | Status |
|---|---|---|---|
| Q-01 | Rules are enforceable (tool where possible) | ✔ | ☐ |
| Q-02 | Consistent with SOLID/DRY/KISS/YAGNI | ✔ | ☐ |
| Q-03 | Enforces "never skip validation/logging/exceptions" | ✔ | ☐ |
| Q-04 | Async safety rules present (no `.Result`/`async void`) | ✔ | ☐ |
| Q-05 | Security coding rules backed by tests/analyzers | ✔ | ☐ |
| Q-06 | Testability enforced (pure domain, IClock) | ✔ | ☐ |
| Q-07 | Every significant decision explained | ✔ | ☐ |

### 18.3 Alignment & Traceability

| # | Check | Mandatory | Status |
|---|---|---|---|
| A-01 | Enforces Clean Architecture (`03`) via arch tests | ✔ | ☐ |
| A-02 | Enforces API contract (`05`) incl. generated types | ✔ | ☐ |
| A-03 | Enforces security design (`06`) at code level | ✔ | ☐ |
| A-04 | Consistent with mandated stack (`00`) | ✔ | ☐ |
| A-05 | Defers CI mechanics to `11`, coverage to `10` | ✔ | ☐ |
| A-06 | Traceability table complete | ✔ | ☐ |

### 18.4 Governance

| # | Check | Mandatory | Status |
|---|---|---|---|
| G-01 | Document control & versioning present | ✔ | ☐ |
| G-02 | Approval sign-off present | ✔ | ☐ |
| G-03 | Ready to proceed to `08_UI_UX.md` on approval | ✔ | ☐ |

---

### ✅ Approval Gate

> **This Coding Standards document (v1.0) is submitted for your approval.** I will **not** begin `08_UI_UX.md` until you approve or request changes.

**Please respond with one of:**
1. **Approved** → I proceed to `08_UI_UX.md`.
2. **Approved with changes** → list changes; I revise then proceed.
3. **Changes required** → list changes; I revise and resubmit this document only.

*End of Document — TAMS-STD-007 v1.0*
