---
description: Run the full post-phase review + QA gap analysis, auto-fix HIGH findings, report the rest.
---

# Phase Review & QA

Run a rigorous end-to-end review of the phase that was just completed (or the phase named in `$ARGUMENTS`, e.g. `/phase-review P2`). This is the automated version of the manual review cycle used throughout this project. Follow it exactly.

## Ground rules (non-negotiable)

1. **Verify every finding yourself before acting on it.** Reviewers (including sub-agents) produce *candidate* findings. Confirm each against the actual code/DB/tests before treating it as real. In this project a past review falsely claimed a migration was missing — it existed and was live. Never fix or report a finding you have not personally reproduced.
2. **Run the real tests, not just reasoning.** Build the solution AND run unit + integration tests. The integration suite has caught bugs invisible to manual checks (e.g. JWT key binding). Use the project's actual verification commands.
3. **Fix policy: auto-fix HIGH, report MEDIUM/LOW.** Fix HIGH-severity findings, re-verify, and leave MEDIUM/LOW as a ranked report for the user to decide — unless the user says otherwise.
4. **Work on a branch.** Do review/fix work on a `review/<phase>` or the current feature branch; never commit review fixes straight to `main`.
5. **No Claude co-author** on any commit.

## Step 1 — Establish scope

- Determine which phase just completed. Read `docs/09_DEVELOPMENT_PLAN.md §6` for that phase's promised epics/features (the authoritative "what should exist" list), and the relevant `docs/02_SRS.md` FRs, `docs/05_API_SPECIFICATION.md` endpoints, `docs/06_SECURITY.md` controls, and `docs/10_TESTING_STRATEGY.md` exit criteria for that phase.
- Identify the code that implements it (src/ + tests/).

## Step 2 — Fan out parallel reviewers (read-only)

Launch independent sub-agents in parallel, each with a distinct lens. Reuse these three dimensions (add more if the phase warrants):

- **Requirements-coverage gap analysis** — for each in-scope requirement, classify Implemented / Partial / Missing vs. the docs. Look hard for missing CRUD verbs, missing endpoints, unenforced rules, missing filters/pagination/sorting, RBAC/data-scope gaps.
- **Correctness & edge cases** — latent bugs, concurrency/idempotency races, error-mapping (DB exceptions → correct status codes), boundary/overnight/timezone math, active-state guards, transaction atomicity.
- **Test & security coverage** — untested domain/handlers vs. coverage targets; missing integration/authz tests; missing security controls (headers, rate limiting, scoping, HTTPS); OWASP matrix items for this phase.

Each agent must: cite file:line, give a concrete failure scenario, assign severity (HIGH/MED/LOW), and mark CONFIRMED vs PLAUSIBLE. Instruct them to verify against actual code, not assume.

## Step 3 — Verify findings yourself

For each candidate finding, independently reproduce it (read the exact code, query the DB, or run a targeted test). Discard false alarms explicitly. Keep only verified findings, re-ranked by real severity.

## Step 4 — Fix HIGH findings

On a branch, fix each verified HIGH finding. Prefer fixes that respect the architecture (Dependency Rule, ports/adapters, CQRS). After each cluster of fixes, rebuild.

## Step 5 — Re-verify (the gate)

- Full solution build: 0 warnings / 0 errors (warnings-as-errors is on).
- Run unit + integration tests; all must pass.
- If the phase has an exit gate in `docs/10 §15` (e.g. P3 = zero-loss fault-injection), run/confirm it.
- Drive the affected flow end-to-end where practical.

## Step 6 — Report

Produce a concise report:
- **False alarms** cleared (with why).
- **HIGH** — fixed + how verified.
- **MEDIUM / LOW** — ranked, with a recommendation on which to fold into which later phase.
- Final test totals and build status.
- Suggested commit (branch, message) — but do not push unless the user asks.

$ARGUMENTS
