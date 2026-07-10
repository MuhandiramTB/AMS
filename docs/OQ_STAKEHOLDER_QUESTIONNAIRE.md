# Open Questions — Stakeholder Questionnaire

## Enterprise Time & Attendance Management System (TAMS)

| Field | Value |
|---|---|
| **Document Title** | Open Questions Stakeholder Questionnaire |
| **Project** | Enterprise Time & Attendance Management System (TAMS) |
| **Document ID** | TAMS-OQ-QUEST-001 |
| **Version** | 1.0 |
| **Status** | Awaiting stakeholder responses |
| **Author** | Principal Software Architect (AI) |
| **Owner** | Product Owner (HR) — coordinates responses |
| **Date** | 2026-07-09 |
| **Classification** | Internal — Confidential |
| **Related Docs** | `01_BRD.md §17`, `02_SRS.md §9.1`, `09_DEVELOPMENT_PLAN.md §13` |

---

## How to use this document

The `01`–`14` documentation set is complete and approved. During its production, **8 open questions (OQ-01…08)** were identified. None blocked documentation — the architecture deliberately isolated each behind configuration or adapters — **but each must be answered before the build phase that depends on it.**

**Instructions for respondents:**
1. Each question has a **suggested respondent** and a **needed-by phase** (the point after which it blocks work).
2. Fill in the **Answer** column (or the free-text area). If a decision is not yet made, note who owns it and a target date.
3. Where a "sensible default" is offered, you may simply confirm it (write *"Confirm default"*) — but please confirm explicitly rather than leaving blank.
4. Return to the Product Owner, who consolidates and shares with the Solution Architect.

> **Why this matters:** every answer converts a *(TBD)* in the documents into a concrete value. Unanswered questions become blocked sprints later; answering them now keeps delivery on track (`09 §13`).

---

## Summary — the 8 open questions

| OQ | Topic | Suggested respondent | Needed before | Blocks if unanswered |
|---|---|---|---|---|
| OQ-01 | ZKTeco device model & SDK/protocol | IT / System Admin + Vendor | **P3 start** (early spike) | Device integration design |
| OQ-02 | Overtime / tolerance / rounding policy | HR + Payroll | **P2 completion** | Attendance calculation config |
| OQ-03 | Leave types, accrual, carry-over | HR | **P4 start** | Leave module |
| OQ-04 | Payroll export format / contract | Payroll | **P5 start** | Payroll export |
| OQ-05 | Data-protection regime & retention | Security / DPO / Legal | **P6** | Security & backup/retention targets |
| OQ-06 | Sizing (employees / devices / sites) | Sponsor / IT | **P6** (targets); earlier for capacity | NFR targets, infra sizing |
| OQ-07 | Self-service leave in initial release? | HR / Sponsor | **P4 start** | Scope of Leave phase |
| OQ-08 | Numeric KPI thresholds & baselines | Sponsor / HR | **P6 UAT** | Acceptance criteria |

---

# OQ-01 — ZKTeco Device Model & Integration Method

**Respondent:** IT / System Administrator (with ZKTeco vendor input)
**Needed before:** Phase 3 (an early spike happens in P1 — the sooner the better)
**Impacts:** `03 §11`, `05 §10.4`, `06 §12`, `04 Devices`, and the highest project risk (RK-01/02)

| # | Question | Answer |
|---|---|---|
| 1.1 | Which exact **ZKTeco model(s)** are deployed (or to be purchased)? | |
| 1.2 | What **integration method** do they support — SDK (which one/version), Push protocol, ADMS, direct TCP, or file export? | |
| 1.3 | Do the devices support **real-time event push**, or only **polled download** of logs? | |
| 1.4 | What **firmware version(s)** are in use? | |
| 1.5 | How are employees **enrolled** on the device (fingerprint, card, face)? What is the **device user id** format? | |
| 1.6 | How many **devices**, and are they on the **same network / VLAN** as the app servers? Any firewall constraints? | |
| 1.7 | Is a **test/lab device** available for the development spike and Staging validation? | |
| 1.8 | Are there **vendor SDK licences / credentials** required, and who owns them? | |

**Notes / decisions:**
_________________________________________________________________

---

# OQ-02 — Overtime, Tolerance & Rounding Policy

**Respondent:** HR + Payroll
**Needed before:** Phase 2 completion (attendance calculation engine)
**Impacts:** `02 §4.4/4.5`, `04 Scheduling`, the `AttendanceCalculator` (`03 ADR-006`)

| # | Question | Answer |
|---|---|---|
| 2.1 | What is the **grace period** for late arrival / early departure (e.g. 10 minutes)? Same for all shifts or per shift? | |
| 2.2 | How is **overtime** defined — beyond shift end, beyond daily hours, beyond weekly hours, or a mix? | |
| 2.3 | Is there a **minimum threshold** before OT counts (e.g. OT only after 30 min extra)? | |
| 2.4 | Are worked hours / OT **rounded** (e.g. to nearest 15 min)? Round up, down, or nearest? | |
| 2.5 | How are **breaks** treated — fixed deduction, paid, or punched? | |
| 2.6 | Different rules for **weekends / public holidays / night shifts**? | |
| 2.7 | Is there a **maximum daily / weekly** worked or OT cap? | |
| 2.8 | Who **approves** overtime, and does it need approval before it counts? | |

**Notes / decisions:**
_________________________________________________________________

---

# OQ-03 — Leave Types, Accrual & Carry-Over

**Respondent:** HR
**Needed before:** Phase 4 start (Leave module)
**Impacts:** `02 §4.7`, `04 Leave`, `13 §11`

| # | Question | Answer |
|---|---|---|
| 3.1 | What **leave types** exist (e.g. Annual, Sick, Casual, Unpaid, Maternity/Paternity, Other)? | |
| 3.2 | What is the **annual entitlement** per type (days)? Does it vary by grade/tenure? | |
| 3.3 | How does leave **accrue** — full allocation at year start, monthly accrual, or on probation completion? | |
| 3.4 | Is **carry-over** to the next year allowed? If so, capped at how many days, and does it expire? | |
| 3.5 | Can leave be taken in **half-days / hours**, or only full days? | |
| 3.6 | What is the **approval workflow** — single manager, multi-level, HR final? | |
| 3.7 | Can leave be **negative / advance** (borrow against future entitlement)? Any override policy? | |
| 3.8 | Does **sick leave** require documentation, and does that affect the system? | |
| 3.9 | How should **public holidays** interact with leave and attendance? | |

**Notes / decisions:**
_________________________________________________________________

---

# OQ-04 — Payroll Export Format / Contract

**Respondent:** Payroll team (and payroll system owner)
**Needed before:** Phase 5 start (Reporting & payroll export)
**Impacts:** `02 FR-RPT-005`, `05 §10.7`, boundary decision (TAMS feeds payroll, `01 OOS-01`)

| # | Question | Answer |
|---|---|---|
| 4.1 | What **payroll system** will consume the export? | |
| 4.2 | What **file format** does it require — CSV, Excel, fixed-width, XML, or a specific template? | |
| 4.3 | What **fields/columns** are required, in what order? (Please attach a sample/spec if available.) | |
| 4.4 | What **identifier** links an employee to payroll (employee number, payroll id)? | |
| 4.5 | What **period** does each export cover (weekly, fortnightly, monthly)? On what **cut-off dates**? | |
| 4.6 | Are worked hours reported in **hours (decimal), minutes, or hours:minutes**? | |
| 4.7 | Should OT be a **separate column / category**? Different OT rates flagged? | |
| 4.8 | How should **leave** appear in the payroll feed (paid/unpaid days)? | |
| 4.9 | Is delivery **manual download**, a **drop folder**, or (future) an integration? | |

**Notes / decisions:**
_________________________________________________________________

---

# OQ-05 — Data-Protection Regime & Retention

**Respondent:** Security Lead / Data Protection Officer / Legal
**Needed before:** Phase 6 (security & backup hardening)
**Impacts:** `06 §8/§14`, `04 §13/§15`, `11 §10`, `14 §6/§7/§10`, RPO/RTO/retention

| # | Question | Answer |
|---|---|---|
| 5.1 | Which **data-protection regime/law** applies (e.g. GDPR, local privacy law)? | |
| 5.2 | What is the **retention period** for: raw punches? attendance records? audit trail? logs? | |
| 5.3 | Which employee fields are considered **sensitive PII** requiring extra protection (encryption/masking)? | |
| 5.4 | Are there **subject-access / erasure** obligations the system must support? | |
| 5.5 | Is **column-level encryption** required for any specific field(s), beyond whole-DB TDE? | |
| 5.6 | What is the acceptable **RPO** (max data loss on disaster) — e.g. 15 min, 1 hour, 1 day? | |
| 5.7 | What is the acceptable **RTO** (max downtime on disaster) — e.g. 1 hour, 4 hours, 1 day? | |
| 5.8 | Any requirement to store data **in a specific country/region** (data residency)? | |
| 5.9 | Is **MFA** required for any roles (e.g. Administrator)? | |

**Notes / decisions:**
_________________________________________________________________

---

# OQ-06 — Sizing (Employees / Devices / Sites)

**Respondent:** Sponsor / IT
**Needed before:** Phase 6 for NFR targets; **earlier** for capacity/infra planning
**Impacts:** `02 §6.1/6.2` NFR targets, `11 §12` scaling, `04 §15` sizing, `14 §12` capacity

| # | Question | Answer |
|---|---|---|
| 6.1 | How many **employees** at launch? Expected in 1 / 3 years? | |
| 6.2 | How many **devices** at launch? Expected growth? | |
| 6.3 | How many **physical sites / locations**? | |
| 6.4 | Approximate **punches per day** (peak)? (e.g. employees × 2–4) | |
| 6.5 | Expected **concurrent users** of the web app (peak)? | |
| 6.6 | Are there **peak windows** (shift start/end) that concentrate load? | |
| 6.7 | Any **availability requirement** (e.g. must be up during 06:00–22:00)? | |
| 6.8 | On-prem hardware available, or is **cloud** planned sooner rather than later? | |

**Notes / decisions:**
_________________________________________________________________

---

# OQ-07 — Employee Self-Service in Initial Release?

**Respondent:** HR / Sponsor
**Needed before:** Phase 4 start (affects Leave scope)
**Impacts:** `02 FR-LV-006`, `08 §2`, `12 §10`

| # | Question | Answer |
|---|---|---|
| 7.1 | Should **employees log in directly** in the first release, or will HR act on their behalf initially? | |
| 7.2 | If self-service: can employees **request leave** themselves? | |
| 7.3 | If self-service: can employees **view their own attendance**? | |
| 7.4 | Do **all** employees get accounts, or only certain grades? | |
| 7.5 | Any concern about employees **seeing their own late/absence** data directly? | |

**Notes / decisions:**
_________________________________________________________________

---

# OQ-08 — Numeric KPI Thresholds & Baselines

**Respondent:** Sponsor / HR
**Needed before:** Phase 6 UAT (acceptance criteria)
**Impacts:** `01 §12` KPIs, `10 §13` UAT, `02 §6` NFR targets

> These convert the BRD's goals into pass/fail acceptance numbers. Where a baseline is unknown, estimate or mark "measure at go-live."

| KPI | Question | Current baseline (as-is) | Target |
|---|---|---|---|
| KPI-01 | Attendance-record **accuracy** target? | | e.g. ≥ 99% |
| KPI-02 | **Manual admin time** now vs target reduction? | | e.g. −70% |
| KPI-03 | **Payroll adjustments** per cycle now vs target? | | |
| KPI-04 | Permanent **punch data loss** tolerated? | | **0 (fixed)** |
| KPI-05 | **% of attendance actions audited**? | | 100% |
| KPI-06 | **Unauthorised-access incidents** tolerated? | | 0 |
| KPI-07 | Acceptable **report generation time**? | | |
| KPI-08 | Target **dashboard adoption** / usage? | | |
| NFR-01 | Acceptable **API response time** (P95)? | | e.g. ≤ 500 ms |
| NFR-03 | Acceptable **dashboard freshness**? | | e.g. ≤ 60 s |

**Notes / decisions:**
_________________________________________________________________

---

## Consolidation & sign-off

| Field | Value |
|---|---|
| Responses consolidated by | Product Owner (HR) |
| Reviewed by | Solution Architect |
| Documents to update on receipt | `02` (NFR targets), `04` (config/seed), `06` (retention/PII), `09` (unblock phases), `10`/`14` (targets) |

| OQ | Status | Answered by | Date | Reflected in docs? |
|---|---|---|---|---|
| OQ-01 | ☐ Open ☐ Answered | | | ☐ |
| OQ-02 | ☐ Open ☐ Answered | | | ☐ |
| OQ-03 | ☐ Open ☐ Answered | | | ☐ |
| OQ-04 | ☐ Open ☐ Answered | | | ☐ |
| OQ-05 | ☐ Open ☐ Answered | | | ☐ |
| OQ-06 | ☐ Open ☐ Answered | | | ☐ |
| OQ-07 | ☐ Open ☐ Answered | | | ☐ |
| OQ-08 | ☐ Open ☐ Answered | | | ☐ |

> **Process note:** as answers arrive, the affected *(TBD)* values in the referenced documents are updated (a minor version bump per document), and `09 §13` open-item tracking is closed out per OQ. No answer is required to *start* documentation-approved work that doesn't depend on it — but each dependent phase's entry is gated on its OQ (per the table above).

*End of Document — TAMS-OQ-QUEST-001 v1.0*
