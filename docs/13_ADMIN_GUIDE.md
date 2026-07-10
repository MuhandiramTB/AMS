# 13 — Administrator Guide

## Enterprise Time & Attendance Management System

| Field | Value |
|---|---|
| **Document Title** | Administrator Guide |
| **Project** | Enterprise Time & Attendance Management System (TAMS) |
| **Document ID** | TAMS-ADMIN-013 |
| **Version** | 1.0 (Draft for Approval) |
| **Status** | Awaiting Approval |
| **Author** | Principal Software Architect (AI) |
| **Owner** | System Administrator / IT Operations |
| **Date** | 2026-07-09 |
| **Classification** | Internal — Confidential |
| **Audience** | **System Administrators** — manage users/roles, devices, business configuration, and administrative housekeeping |
| **Standards** | Least-privilege administration, task-oriented procedures, secure-by-default operations, ITIL change concepts |
| **Predecessor Docs** | `01`–`12` (all approved) |
| **Successor Docs** | `14_MAINTENANCE_GUIDE.md` |

> **Scope of this document.** This is the **task-based guide for TAMS administrators**: managing users/roles/permissions, registering and operating ZKTeco devices, employee↔device enrolment, configuring business rules (shifts, tolerances, leave types), reference data, and administrative oversight (audit, exports governance). It sits **between** the end-user guide (`12`) and the maintenance/operations guide (`14`).
>
> **Boundary with other docs.** This covers **application-level administration** performed *inside* TAMS by an Administrator role. It does **not** cover infrastructure operations — deployment, backups, monitoring, patching, DR — which are **IT Operations** tasks in `14_MAINTENANCE_GUIDE.md`. It does **not** redefine security policy (`06`) or the API (`05`); it explains how an admin *uses* the features those define. Where a task is destructive or security-sensitive, the guide flags it and states the safe procedure.
>
> **Golden rule.** Everything an administrator does is **audited** (`06 §11`). Administrators operate under the same least-privilege and accountability model as everyone else — there is no "invisible" admin action.

---

## Document Control

### Revision History

| Version | Date | Author | Description |
|---|---|---|---|
| 1.0 | 2026-07-09 | AI Architect | First complete admin guide derived from approved User Manual v1.0 |

### Approval Sign-off

| Role | Name | Signature | Date |
|---|---|---|---|
| System Administrator | _TBD_ | | |
| IT Operations Lead | _TBD_ | | |
| Security Lead | _TBD_ | | |
| Solution Architect | _TBD_ | | |

---

## Table of Contents

1. [Administrator Role & Responsibilities](#1-administrator-role--responsibilities)
2. [First-Time Setup Checklist](#2-first-time-setup-checklist)
3. [Managing Users](#3-managing-users)
4. [Managing Roles & Permissions](#4-managing-roles--permissions)
5. [Managing Departments](#5-managing-departments)
6. [Managing Employees](#6-managing-employees)
7. [Managing ZKTeco Devices](#7-managing-zkteco-devices)
8. [Employee ↔ Device Enrollment](#8-employee--device-enrollment)
9. [Managing Shifts & Assignments](#9-managing-shifts--assignments)
10. [Configuring Business Rules (Rules-as-Data)](#10-configuring-business-rules-rules-as-data)
11. [Managing Leave Types & Balances](#11-managing-leave-types--balances)
12. [Monitoring Device Health & Sync](#12-monitoring-device-health--sync)
13. [Reviewing the Audit Trail](#13-reviewing-the-audit-trail)
14. [Exports & Data Governance](#14-exports--data-governance)
15. [Security Administration Practices](#15-security-administration-practices)
16. [Admin Troubleshooting](#16-admin-troubleshooting)
17. [Boundaries: What Belongs to IT Ops (`14`)](#17-boundaries-what-belongs-to-it-ops-14)
18. [Traceability (Requirements → Admin Tasks)](#18-traceability-requirements--admin-tasks)
19. [Glossary](#19-glossary)
20. [Documentation Review Checklist](#20-documentation-review-checklist)

---

# 1. Administrator Role & Responsibilities

The Administrator keeps TAMS **correctly configured, securely accessible, and reliably capturing** attendance.

| Responsibility | Covered in |
|---|---|
| User & access management | §3, §4 |
| Organisation & workforce data | §5, §6 |
| Device operation (ZKTeco) | §7, §8, §12 |
| Business-rule configuration | §9, §10, §11 |
| Oversight (audit, exports) | §13, §14 |
| Security practices | §15 |

**What the Administrator does *not* do** (→ `14_MAINTENANCE_GUIDE.md`): server/OS patching, database backups, deployments, monitoring infrastructure, disaster recovery.

**Decision — separate the *application administrator* from the *infrastructure operator*.** These are different jobs with different skills and risks. The application admin (this guide) configures TAMS *through the UI/API* under the audited Administrator role; IT Operations (`14`) manages the *platform*. Splitting them cleanly (a) keeps this guide focused and usable, (b) supports least-privilege (an app admin needs no server access), and (c) matches the real organisational division of duties. Where they meet (e.g. a device needs a firewall change), the guide hands off explicitly (§17).

---

# 2. First-Time Setup Checklist

After IT Operations has deployed TAMS (`11`), the Administrator performs initial configuration **in this order** (each step depends on the previous):

- [ ] **2.1** Sign in with the bootstrap admin account; **change the password immediately** (`04 §12`).
- [ ] **2.2** Review/confirm **roles & permissions** (§4) match your organisation.
- [ ] **2.3** Create **departments** (organisation structure) (§5).
- [ ] **2.4** Create **shifts** and set tolerances/OT rules (§9, §10).
- [ ] **2.5** Add **employees** and assign them to departments & shifts (§6, §9).
- [ ] **2.6** Register **ZKTeco devices** and test connectivity (§7).
- [ ] **2.7** Set up **employee↔device enrollment** (§8).
- [ ] **2.8** Configure **leave types** and initial **balances** (§11).
- [ ] **2.9** Create **user accounts** for HR/managers/auditors with correct roles (§3).
- [ ] **2.10** Verify a **test punch** flows device→record and produces audit (§12).
- [ ] **2.11** Confirm device **monitoring/alerts** are active (§12, with IT Ops).

**Decision — order setup by dependency and end it with a proven punch.** Departments must exist before employees; shifts before assignments; devices before enrollment. Following the dependency order avoids "can't save — no department" dead-ends. The checklist deliberately ends (2.10) with a **verified end-to-end punch** — the same principle as the go-live runbook (`11 §14`): configuration isn't "done" until the system's core job demonstrably works.

---

# 3. Managing Users

**What a "user" is:** a login account with one or more roles. A user *may* be linked to an employee.

## 3.1 Create a user

1. Go to **Admin → Users & Roles → Users**.
2. Select **New user**.
3. Enter username, email, and (optionally) link to an **employee**.
4. Assign one or more **roles** (§4).
5. Save. The user receives/gets an initial password and is **forced to change it on first login** (`06 §4`).

## 3.2 Manage a user

| Task | How |
|---|---|
| Edit roles | Users → select user → adjust roles → save |
| Deactivate | Set inactive (soft) — login blocked, history kept (never hard-delete) |
| Reset password | Trigger reset; user sets a new one |
| Unlock account | Clear lockout after too many failed logins (`06 §4`) |
| Revoke access immediately | Deactivate + (session/refresh tokens invalidated) (`06 §6`) |

**Decision — deactivate, never delete; revocation is immediate.** Deleting a user would orphan their audit history and break accountability. Users are therefore **soft-deactivated** (login blocked, records retained). For a leaver or compromise, deactivation **immediately** blocks access and invalidates refresh tokens (`06 §6/§16`) — the admin's fastest containment lever. This preserves the audit trail (G-05) while giving instant security response.

---

# 4. Managing Roles & Permissions

TAMS uses **permission-based roles**: a role is a bundle of permissions; a user gets roles.

| Default role | Purpose (from `02 §4.1`) |
|---|---|
| **Administrator** | Full configuration & administration |
| **HR Officer** | Employee/attendance/leave operations |
| **Manager** | Team oversight & approvals |
| **Employee** | Self-service |
| **Auditor** | Read-only audit & reports |

## 4.1 Assign roles

1. **Admin → Users & Roles → Roles** to view roles and their permissions.
2. Assign roles to users in **Users** (§3).

## 4.2 Principle to follow

> **Least privilege:** give each user the *minimum* role(s) for their job. Prefer adding a specific role over granting Administrator.

**Decision — grant the narrowest role, and treat Administrator as rare.** The most common access-control mistake is over-granting "admin to be safe." The guide's standing instruction is **least privilege** (`06 §5`, SP-03): most staff are Employees; HR/Managers get their specific roles; Administrator is reserved for the few who genuinely configure the system. Fewer admins = smaller attack surface and clearer accountability. Changing a role's *permission set* is a security-sensitive change — coordinate with the Security Lead.

---

# 5. Managing Departments

1. **Admin/People → Departments**.
2. **New department**: enter code & name; optionally set a **parent** (for hierarchy).
3. Edit or **deactivate** (soft) as the org changes.

**Rules to know:**
- A department code is **unique**.
- You **cannot deactivate** a department that still has active employees — **reassign them first** (`04 §7`, FR-DEP-003).
- Avoid creating cycles in the hierarchy (a department can't be its own ancestor).

---

# 6. Managing Employees

1. **People → Employees → New employee**.
2. Enter employee number (**unique**), name, email, and **primary department** (**required** — every employee has exactly one, BRULE-01).
3. Set status; assign a **shift** (§9) and set up **device enrollment** (§8).
4. Save. Validation prevents incomplete/incorrect records.

| Task | Note |
|---|---|
| Deactivate employee | Soft (status/inactive); records & history retained |
| Change department | Reassign primary department |
| Status history | Status changes are tracked over time (FR-EMP-005) |

> Bulk import of employees, if provided, follows the same validation rules — verify data quality first (RK-07).

**Decision — enforce "one primary department + unique number" at entry, and never hard-delete.** These invariants (BRULE-01, FR-EMP-002) exist because attendance and reporting depend on them; catching violations at data-entry (clear validation message) is far cheaper than discovering a mis-departmented employee at payroll. Soft-deactivation keeps historical attendance and audit intact for leavers — essential for compliance and any back-dated payroll query.

---

# 7. Managing ZKTeco Devices

Devices are the source of attendance punches, so device administration is critical.

## 7.1 Register a device

1. **Admin → Devices → Register**.
2. Enter the device **name**, **serial number**, and network details (**IP/port**) as provided.
3. Save (device starts **enabled**).
4. Select **Test connection** — confirm the device is reachable.

## 7.2 Manage devices

| Task | How | Note |
|---|---|---|
| Enable/disable | Toggle on the device | Disabled devices are not polled |
| Edit details | Update name/network | |
| Test connection | On-demand reachability check | Returns OK or unreachable |
| Sync now | Trigger an immediate sync | Signals the background worker; runs safely without blocking you (`05 §10.4`) |
| View sync state | See last watermark, last success, failure count | Health insight (§12) |

**Rules to know:**
- Serial number is **unique** — a device can only be registered once (prevents fake/rogue devices, `06 §12`).
- Only **registered** devices are accepted — unknown devices are ignored (allow-list).

**Decision — devices are allow-listed and only registered serials are trusted.** Because a rogue device could inject fake punches (a security *and* data-integrity threat, `06 §12`), the admin must register each device by serial, and TAMS ignores anything not on the list. "Test connection" and "Sync now" let the admin verify and drive capture safely — the sync runs in the background worker (`03 ADR-002`), so the admin's screen never freezes waiting on a device. This gives the admin control without compromising the resilience design.

---

# 8. Employee ↔ Device Enrollment

For a punch to be attributed to the right person, each employee must be **enrolled** on the device(s) they use.

1. Open the **employee** (§6) → **Enrollments** (or **Device → Enrollments**).
2. Add an enrollment: choose the **device** and the employee's **ID on that device** (the device user id).
3. Save.

**Rules to know:**
- A `(device, device-user-id)` pair maps to **exactly one employee** (BRULE-09). The system prevents assigning the same device slot to two people.
- An employee can be enrolled on **multiple devices**.
- If a punch arrives for an unknown/unenrolled device id, it is captured but flagged as **unresolved** for you to fix — it is never silently attributed to the wrong person.

**Decision — enforce unique enrollment mapping and never guess an owner.** Attributing a punch to the wrong employee corrupts pay and trust. The unique `(device, device-user-id)→employee` rule (BRULE-09) guarantees each punch resolves to one person, and unresolved punches are **surfaced, not guessed** — the admin fixes the enrollment, and the punch (already safely stored) resolves correctly. This protects both data integrity and fairness.

---

# 9. Managing Shifts & Assignments

## 9.1 Create a shift

1. **Admin → Shifts → New shift**.
2. Set **start/end time**, **break minutes**, and **grace** (tolerance) for in/out.
3. For overnight shifts, set an end time earlier than the start — the system treats it as crossing midnight (FR-SFT-005).
4. Configure **overtime rules** (§10) if applicable.

## 9.2 Assign a shift

1. **Admin → Shift Assignments → New assignment**.
2. Choose a **shift** and assign to an **employee** *or* a **department** (not both).
3. Set the **effective from** date (and optional **to** date).

**Rules to know:**
- Assignments are **effective-dated** — set the date the shift starts applying.
- Overlapping active assignments for the same target are rejected.

**Decision — effective-dating is set by the admin and matters.** When you assign a shift **from a date**, attendance before that date still uses whatever shift applied *then* (`04`, FR-SFT-003). This means changing someone's shift never silently rewrites their past hours — historical records and any recomputation stay correct. Admins should always set the correct effective date rather than assuming "now," especially for back-dated changes.

---

# 10. Configuring Business Rules (Rules-as-Data)

Many policies are **configuration**, not code — you change them in TAMS without a software release.

| Configurable rule | Where | Notes |
|---|---|---|
| Shift grace/tolerance | On the shift (§9) | Late/early thresholds |
| Overtime policy | On the shift / configuration | Pending organisation policy (OQ-02) |
| Leave types & accrual | Leave types (§11) | Pending policy (OQ-03) |
| Other business settings | **Admin → Configuration** | Key/value, validated |

**How to change a rule:**
1. **Admin → Configuration** (or the relevant shift/leave screen).
2. Change the value; the system **validates** it (rejects unsafe values).
3. Save — the change is **audited** and takes effect without redeployment.

**Decision — business rules are admin-editable data, not developer-owned code.** Tolerances, overtime policy and leave rules change with business policy, not software versions. Making them **configuration** (`04 §12`, FR-ADM-003, `11 §5`) means the admin adjusts policy directly — no code change, no deployment, no waiting. Every change is validated (no unsafe values) and audited (who changed what policy, when). This is why the open policy questions (OQ-02/OQ-03) don't block the system: answers are entered as data.

---

# 11. Managing Leave Types & Balances

## 11.1 Leave types

1. **Admin → Leave Types → New type** (e.g. Annual, Sick).
2. Set the code, name, and accrual/policy details (per organisation policy, OQ-03).
3. Deactivate types no longer used (soft).

## 11.2 Balances

- Balances are held **per employee, per leave type, per year**.
- Set initial **entitlements**; the system tracks **used** vs **entitled** as leave is approved.
- Approvals beyond balance are **blocked** unless an override policy applies (BRULE-07).

**Decision — balances are enforced, not advisory.** Because over-granting leave has real cost, the system **blocks** approval beyond the available balance (FR-LV-004) unless policy explicitly allows an override. The admin sets entitlements accurately; the system does the arithmetic and enforcement — removing a common source of manual error and dispute.

---

# 12. Monitoring Device Health & Sync

Keeping devices healthy is how the admin guarantees no attendance is missed.

1. **Admin → Devices** shows each device's **status** (🟢 online / 🔴 offline) and **last seen**.
2. Open a device → **Sync state** to see the last successful sync, watermark, and consecutive-failure count.
3. When a device shows **offline** or repeated failures:
   - Use **Test connection** to confirm.
   - Check the device is powered and on the network (physical/network issues → IT Ops, §17).
   - The system keeps trying (retry/backoff) and **will recover missed punches** once the device is back (offline recovery) — no data is lost in the meantime.
4. Respond to **alerts** for devices unreachable beyond the threshold (FR-ZK-011).

```text
Device offline?  ──▶ Test connection ──▶ still down? ──▶ check power/network
                                                        │ (network/firewall → IT Ops §17)
   System meanwhile: retries + buffers ──▶ device back ──▶ missed punches recovered ✓
```

**Decision — give the admin visibility and reassurance: an outage is recoverable, not lost data.** The admin's job during an outage is to **restore the device**, not to panic about lost punches — because the system buffers and recovers on reconnect (`03 §10.2`, ADR-011, KPI-04). The guide makes this explicit so the admin acts calmly and correctly: fix the device; trust the recovery. Alerts (FR-ZK-011) ensure prolonged outages are noticed promptly, turning the backend's resilience into operational action.

---

# 13. Reviewing the Audit Trail

1. **Admin → Audit Log**.
2. Filter by **entity**, **user**, or **date range**.
3. Review who did what, when, with before/after values.

**Uses for the admin:**
- Investigate "who changed this record and why."
- Support security or compliance queries.
- Confirm configuration/permission changes.

> The audit log is **read-only for everyone**, including administrators — it cannot be edited or deleted from TAMS by design (`06 §11`).

**Decision — even administrators cannot alter the audit trail.** Admin power stops at the audit log: it is append-only for *everyone* (`04 §13`, `06 §11`). This is deliberate — if admins could edit audit records, the trail would be worthless as evidence. The admin *reads* the audit to investigate, but the record of their own actions is equally immutable. This protects the integrity guarantee (G-05) against insider risk.

---

# 14. Exports & Data Governance

- HR/authorised roles produce reports and the **payroll export** (`12 §14`).
- Exports contain personal and attendance data — handle per your **data-protection policy** (OQ-05, `06 §14`).
- Export actions are **audited** (who exported what, when).

**Admin responsibilities:**
- Ensure only appropriate roles have export permission (§4).
- Reinforce that exported files leave TAMS's controls — they must be stored/shared securely.

**Decision — treat exported data as leaving the security perimeter.** Inside TAMS, data is protected by RBAC, encryption and audit. The moment it's exported to a file, those controls no longer apply. The admin governs this by **limiting who can export** and reinforcing secure handling of files — because the biggest data-leak risk in any reporting system is uncontrolled exports, not the database itself.

---

# 15. Security Administration Practices

| Practice | Why |
|---|---|
| **Least privilege** — narrowest roles; few Administrators | Smaller attack surface (`06 §5`) |
| **Prompt de-provisioning** — deactivate leavers immediately | Close access fast (`06 §16`) |
| **Review access periodically** — audit who has which role | Catch privilege creep |
| **Strong bootstrap** — change the seeded admin password first | No default-credential risk |
| **Don't share accounts** — one login per person | Accountability (audit) |
| **Watch alerts** — auth-failure spikes, device outages | Early detection |
| **Coordinate sensitive changes** — permission-set edits with Security Lead | Governance |
| **Never store secrets in TAMS data** | Secrets belong in the secret store (`06 §9`, IT Ops) |

**Decision — the admin is a security actor, and the guide says so.** An administrator holds the keys to access and configuration, so their *habits* are a security control. Codifying them — least privilege, prompt de-provisioning, no shared accounts, periodic access review — turns good intentions into standard practice and makes the admin an active participant in the security model (`06`), not a bypass around it.

---

# 16. Admin Troubleshooting

| Symptom | Likely cause | Action |
|---|---|---|
| Device shows offline | Power/network/firewall | Test connection; check device; escalate network to IT Ops (§17) |
| Punches not appearing | Device offline / enrollment missing / employee not enrolled | Check device sync state (§12); verify enrollment (§8) |
| Punch "unresolved" | Device id not mapped to an employee | Add/fix enrollment (§8) |
| User can't log in | Wrong credentials / locked / deactivated | Reset password / unlock / reactivate (§3) |
| User "no permission" | Missing role | Assign correct role (§4) |
| Can't deactivate department | Active employees remain | Reassign employees first (§5) |
| Can't save employee | Missing primary department / duplicate number | Fix per validation message (§6) |
| Leave approval blocked | Over balance | Check balance; apply override if policy allows (§11) |
| Rule change not taking effect | Invalid value rejected, or wrong scope | Recheck configuration validation (§10) |
| System-wide slowness/outage | Infrastructure | **Escalate to IT Ops (`14`)** |

**Decision — the troubleshooting table separates "admin can fix" from "escalate to IT Ops."** Most day-to-day issues (offline device, missing enrollment, locked user, over-balance leave) are **application** problems the admin resolves here. Genuinely infrastructural problems (system down, network, performance-for-everyone) are explicitly routed to `14`. Drawing this line prevents the admin either flailing at a server problem or escalating a simple enrollment fix — the right person handles the right issue.

---

# 17. Boundaries: What Belongs to IT Ops (`14`)

| Belongs to Administrator (this guide) | Belongs to IT Operations (`14`) |
|---|---|
| Users, roles, permissions | Server/OS/runtime patching |
| Devices (register/test/sync in-app) | Network/firewall to device VLAN |
| Enrollments, shifts, employees | Database backups & restore |
| Business rules/config, leave types | Deployments & releases |
| Audit review, export governance | Monitoring infrastructure, alert plumbing |
| In-app security practices | Certificates, secret store, DR |

> When an admin task hits an infrastructure limit (e.g. a device can't be reached because a firewall rule is missing), **hand off to IT Ops with the details** (device, error, correlation id).

---

# 18. Traceability (Requirements → Admin Tasks)

| Requirement | Admin task |
|---|---|
| FR-ADM-001 (users/roles) | §3, §4 |
| FR-ADM-002 (devices) | §7, §12 |
| FR-ADM-003 (config rules-as-data) | §10 |
| FR-ADM-004/005 (validated & audited config) | §10, §13 |
| FR-EMP-* / FR-DEP-* | §5, §6 |
| BRULE-01/09 (dept + enrollment uniqueness) | §6, §8 |
| FR-SFT-003/005 (effective-dated, overnight) | §9 |
| FR-LV-004 (balance enforcement) | §11 |
| FR-ZK-010/011 (device mgmt & alerts) | §7, §12 |
| FR-AUD-005 (audit read) | §13 |
| BR-050/`06 §5` (least privilege) | §4, §15 |
| KPI-04 (zero loss visibility) | §12 |

---

# 19. Glossary

Inherits prior docs. Admin-relevant terms:

| Term | Definition |
|---|---|
| **User** | A login account (may link to an employee). |
| **Role** | A named bundle of permissions. |
| **Permission** | A specific allowed action. |
| **Enrollment** | Mapping of an employee to their id on a device. |
| **Device user id** | The identifier a person has *on the device*. |
| **Watermark** | The device's last-synced pointer (sync state). |
| **Effective-dated** | A setting valid from (and optionally to) a date. |
| **Rules-as-data** | Business policy stored as editable configuration. |
| **Soft deactivate** | Mark inactive without deleting (history kept). |
| **Allow-list** | Only registered devices are trusted. |

---

# 20. Documentation Review Checklist

**Reviewer instructions:** mark ✅ Pass / ⚠️ Needs change / ❌ Fail. Approved when all **Mandatory** items pass.

### 20.1 Completeness

| # | Check | Mandatory | Status |
|---|---|---|---|
| C-01 | Admin role & responsibilities defined | ✔ | ☐ |
| C-02 | First-time setup checklist provided | ✔ | ☐ |
| C-03 | User management covered | ✔ | ☐ |
| C-04 | Roles & permissions covered | ✔ | ☐ |
| C-05 | Department & employee management covered | ✔ | ☐ |
| C-06 | Device management covered | ✔ | ☐ |
| C-07 | Enrollment covered | ✔ | ☐ |
| C-08 | Shifts & assignments covered | ✔ | ☐ |
| C-09 | Business-rule configuration covered | ✔ | ☐ |
| C-10 | Leave types & balances covered | ✔ | ☐ |
| C-11 | Device health monitoring covered | ✔ | ☐ |
| C-12 | Audit review covered | ✔ | ☐ |
| C-13 | Exports & data governance covered | ✔ | ☐ |
| C-14 | Security admin practices covered | ✔ | ☐ |
| C-15 | Admin troubleshooting covered | ✔ | ☐ |
| C-16 | IT Ops boundary defined | ✔ | ☐ |

### 20.2 Quality & Soundness

| # | Check | Mandatory | Status |
|---|---|---|---|
| Q-01 | Task-oriented, ordered by dependency | ✔ | ☐ |
| Q-02 | Least-privilege reinforced throughout | ✔ | ☐ |
| Q-03 | Soft-deactivate (never hard-delete) reinforced | ✔ | ☐ |
| Q-04 | Device allow-list & enrollment uniqueness explained | ✔ | ☐ |
| Q-05 | Outage = recoverable, not lost (reassurance) | ✔ | ☐ |
| Q-06 | Admin cannot alter audit trail (stated) | ✔ | ☐ |
| Q-07 | Config changes are validated & audited | ✔ | ☐ |
| Q-08 | Every significant decision explained | ✔ | ☐ |

### 20.3 Alignment

| # | Check | Mandatory | Status |
|---|---|---|---|
| A-01 | Consistent with security/RBAC (`06`) | ✔ | ☐ |
| A-02 | Consistent with device design (`03`/`04`) | ✔ | ☐ |
| A-03 | Uses admin features from API (`05`) | ✔ | ☐ |
| A-04 | Clear handoff to `14` for infrastructure | ✔ | ☐ |
| A-05 | OQ-02/03/05 flagged as policy inputs | ✔ | ☐ |
| A-06 | Traceability table complete | ✔ | ☐ |

### 20.4 Governance

| # | Check | Mandatory | Status |
|---|---|---|---|
| G-01 | Document control & versioning present | ✔ | ☐ |
| G-02 | Approval sign-off present | ✔ | ☐ |
| G-03 | Ready to proceed to `14_MAINTENANCE_GUIDE.md` on approval | ✔ | ☐ |

---

### ✅ Approval Gate

> **This Administrator Guide (v1.0) is submitted for your approval.** I will **not** begin `14_MAINTENANCE_GUIDE.md` until you approve or request changes.

**Please respond with one of:**
1. **Approved** → I proceed to `14_MAINTENANCE_GUIDE.md` (the final document).
2. **Approved with changes** → list changes; I revise then proceed.
3. **Changes required** → list changes; I revise and resubmit this document only.

*End of Document — TAMS-ADMIN-013 v1.0*
