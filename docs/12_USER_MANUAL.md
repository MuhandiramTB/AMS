# 12 — User Manual (End-User Guide)

## Enterprise Time & Attendance Management System

| Field | Value |
|---|---|
| **Document Title** | User Manual (End-User Guide) |
| **Project** | Enterprise Time & Attendance Management System (TAMS) |
| **Document ID** | TAMS-USER-012 |
| **Version** | 1.0 (Draft for Approval) |
| **Status** | Awaiting Approval |
| **Author** | Principal Software Architect (AI) |
| **Owner** | Product Owner (HR) / Training Lead |
| **Date** | 2026-07-09 |
| **Classification** | Internal |
| **Audience** | **End users** — HR Officers, Department Managers, Employees, Auditors (non-technical) |
| **Standards** | Task-oriented technical writing (**Minimalism / Every Page is Page One**), plain language, accessibility-aware |
| **Predecessor Docs** | `01`–`11` (all approved) |
| **Successor Docs** | `13_ADMIN_GUIDE.md`, `14_MAINTENANCE_GUIDE.md` |

> **Scope of this document.** This is the **task-based guide for everyday users** of TAMS. It explains *how to do the jobs* each role performs — logging in, reading the dashboard, resolving attendance exceptions, correcting attendance, requesting/approving leave, running reports/exports, and understanding statuses. It is written for **non-technical staff**.
>
> **Boundary with other docs.** This covers *using* the system. It does **not** cover system administration (users, roles, devices, configuration → `13_ADMIN_GUIDE.md`) or operations/maintenance (backups, monitoring, troubleshooting infrastructure → `14_MAINTENANCE_GUIDE.md`). Where a task requires admin rights, it points to `13`.
>
> **Convention.** Screens described here follow the UI/UX spec (`08`). Wording, labels and layouts are the design intent; final wording may be refined during build. Steps are numbered; **role required** is stated for each task so users know if a task applies to them.

---

## Document Control

### Revision History

| Version | Date | Author | Description |
|---|---|---|---|
| 1.0 | 2026-07-09 | AI Architect | First complete user manual derived from approved Deployment guide v1.0 |

### Approval Sign-off

| Role | Name | Signature | Date |
|---|---|---|---|
| Product Owner (HR) | _TBD_ | | |
| Training Lead | _TBD_ | | |
| HR Officer (reviewer) | _TBD_ | | |
| Accessibility reviewer | _TBD_ | | |

---

## Table of Contents

1. [Welcome & Who This Guide Is For](#1-welcome--who-this-guide-is-for)
2. [Key Concepts (Plain Language)](#2-key-concepts-plain-language)
3. [Getting Started: Logging In](#3-getting-started-logging-in)
4. [Finding Your Way Around](#4-finding-your-way-around)
5. [Understanding the Dashboard](#5-understanding-the-dashboard)
6. [Understanding Attendance Statuses](#6-understanding-attendance-statuses)
7. [Task: Review & Resolve Attendance Exceptions (HR)](#7-task-review--resolve-attendance-exceptions-hr)
8. [Task: Correct an Attendance Record (HR)](#8-task-correct-an-attendance-record-hr)
9. [Task: View Employees & Departments (HR)](#9-task-view-employees--departments-hr)
10. [Task: Request Leave (Employee)](#10-task-request-leave-employee)
11. [Task: Approve or Reject Leave (Manager)](#11-task-approve-or-reject-leave-manager)
12. [Task: View Your Own Attendance (Employee)](#12-task-view-your-own-attendance-employee)
13. [Task: See Your Team (Manager)](#13-task-see-your-team-manager)
14. [Task: Run Reports & Exports](#14-task-run-reports--exports)
15. [Task: Review the Audit Trail (Auditor)](#15-task-review-the-audit-trail-auditor)
16. [Notifications & Alerts](#16-notifications--alerts)
17. [Accessibility Features](#17-accessibility-features)
18. [Frequently Asked Questions](#18-frequently-asked-questions)
19. [Troubleshooting (User-Level)](#19-troubleshooting-user-level)
20. [Glossary](#20-glossary)
21. [Documentation Review Checklist](#21-documentation-review-checklist)

---

# 1. Welcome & Who This Guide Is For

Welcome to **TAMS**, the Time & Attendance Management System. TAMS records when people start and finish work (captured automatically from the fingerprint/biometric devices), works out worked hours against each person's shift, tracks leave, and gives managers and HR clear, accurate information.

This guide is organised by **the job you need to do**, grouped by role. Use the table to jump to your tasks.

| If you are a… | You will mostly use… |
|---|---|
| **HR Officer** | Exceptions worklist (§7), corrections (§8), employees (§9), reports (§14) |
| **Department Manager** | Team view (§13), leave approvals (§11), team reports (§14) |
| **Employee** | Your attendance (§12), leave requests (§10) |
| **Auditor** | Audit trail (§15), reports (§14) |

**Decision — organise the manual by task and role, not by menu.** Users come to a manual with a *goal* ("how do I approve leave?"), not a desire to read about a menu. A task-oriented, role-grouped structure (minimalist technical-writing practice) lets each user find *their* jobs fast and ignore the rest — which also keeps the manual usable as the system grows. Each task states the **role required** so no one follows steps they can't complete.

---

# 2. Key Concepts (Plain Language)

A few ideas make everything else easy to understand:

| Term | What it means for you |
|---|---|
| **Punch** | A single check-in or check-out at a device (e.g. arriving at 08:00). |
| **Shift** | Your expected working window (e.g. 09:00–17:00) with allowed grace time. |
| **Attendance record** | The system's daily summary for one person: when they came in, left, hours worked, and any issues. |
| **Exception** | A day that needs a human to look at it — e.g. someone forgot to check out. |
| **Correction** | An authorised fix to a record. Every correction needs a reason and is kept on record. |
| **Leave** | Approved time off (annual, sick, etc.), which the system counts correctly. |
| **Role** | What you're allowed to see and do. You only see the parts of TAMS your role allows. |

**Decision — explain concepts once, up front, in plain language.** Attendance software has a handful of domain concepts (punch vs record, exception, correction) that, once understood, make every task obvious. Defining them plainly at the start — and *why they matter to the user* — avoids repeating explanations in every task and reduces support questions. This mirrors the trust model built into the system: users understand that records come *from* punches and that corrections are always tracked (§8).

---

# 3. Getting Started: Logging In

**Role required:** Any user.

1. Open your web browser and go to the TAMS web address (provided by your administrator).
2. Enter your **username** and **password**.
3. Select **Sign in**.
4. On first login you may be asked to **change your password** — choose a strong one you don't use elsewhere.

**If sign-in fails:**
- Check your username/password. The message is intentionally general for security — it won't say which field is wrong.
- After several failed attempts your account may be **temporarily locked**. Wait and try again, or contact your administrator (`13`).

**To sign out:** select your name (top-right) → **Log out**. Always log out on shared computers.

> 🔒 **Your access is limited to your role.** If you don't see a menu or button described in this guide, your role doesn't include that task — that's expected, not a fault.

**Decision — set the security expectation at login.** Two things confuse users most at login: the deliberately vague "invalid credentials" message and account lockout. Explaining *why* they exist (security, `06 §4`) up front turns a frustrating surprise into understood, trusted behaviour — and pre-empts a common support call.

---

# 4. Finding Your Way Around

After signing in you'll see the main screen:

```text
┌───────────────────────────────────────────────────────────────┐
│ TAMS    🔍 search        🔔 notifications   [Your name ▾]       │  ← top bar
├───────────┬───────────────────────────────────────────────────┤
│ Dashboard │                                                     │
│ Attendance│      (the page you're working on appears here)      │
│ Leave     │                                                     │
│ People    │                                                     │
│ Reports   │                                                     │
│ …         │                                                     │
└───────────┴───────────────────────────────────────────────────┘
     ↑ side menu (only shows what your role can use)
```

| Area | What it's for |
|---|---|
| **Top bar** | Search, notifications (🔔), your account & log out |
| **Side menu** | Move between the main areas (only your permitted areas appear) |
| **Breadcrumbs** | Show where you are; select to go back |
| **Main area** | The current page, with its main action usually top-right |

**Tip:** Every page has its own web address — you can bookmark a page you use often.

---

# 5. Understanding the Dashboard

**Role required:** Any user (you see data for your scope — your team, or all, depending on role).

The Dashboard is your starting point each day. It shows:

- **Summary tiles** — e.g. *Present*, *Absent*, *Late*, and *Open exceptions*.
- **Attendance by department** — a quick visual of who's in.
- **Shortcuts** to the day's work.

**Tiles are clickable.** Selecting a tile (e.g. **Open exceptions: 12**) takes you straight to that filtered list — the fastest way into your work.

> The dashboard updates automatically and shows how recently it refreshed (e.g. "updated 30s ago").

**Decision — teach users that the dashboard is a launchpad, not just a display.** The single biggest efficiency win for daily users is knowing that **every tile drills down** into the actual work (`08 §10.2`). Stating this explicitly turns the dashboard from a passive scoreboard into the fastest route into the day's tasks — directly supporting the goal of reducing admin effort (G-02).

---

# 6. Understanding Attendance Statuses

Statuses use **both colour and a label/icon**, so they're clear for everyone (including on printouts).

| Status | Looks like | Meaning |
|---|---|---|
| **Present** | 🟢 Present | Normal attendance recorded |
| **Late** | 🟠 Late | Arrived after the shift's grace time |
| **Early leave** | 🟠 Early | Left before shift end |
| **Absent** | 🔴 Absent | No attendance and no approved leave |
| **On leave** | 🔵 Leave | Covered by approved leave |
| **Exception** | ⚠ Exception | Needs review (e.g. missing check-out) |

A record can move through stages: **Pending → Processed → (Exception → Under review → Corrected) → Finalized**.

---

# 7. Task: Review & Resolve Attendance Exceptions (HR)

**Role required:** HR Officer (Managers can view their team's; Auditors can view read-only.)

This is the most common HR task: dealing with days the system has flagged.

1. From the Dashboard, select the **Open exceptions** tile — *or* go to **Attendance → Exceptions**.
2. The worklist shows unresolved exceptions. Use the **filters** (department, date range, type) to narrow the list.
3. Select **Review** on a row to open the record.
4. Read the details — you'll see the **raw check-ins/outs** on one side and the **system's calculation** on the other, with the problem highlighted (e.g. *missing check-out*).
5. Decide what to do:
   - If the cause is clear → **correct the record** (see §8).
   - If you need more information → add a **note** and leave it open.
6. When the day is right, select **Mark resolved** (add notes if useful).
7. The exception disappears from the "unresolved" list.

```text
Dashboard ──(Open exceptions)──▶ Exceptions worklist ──(Review)──▶
   Record detail (raw vs computed) ──(Correct if needed)──▶ Mark resolved ✓
```

**Decision — present exceptions as a *worklist* and walk the user through the whole loop.** Exceptions are the heart of daily HR work, so the manual treats the *worklist → review → correct → resolve* loop as one continuous, guided flow rather than isolated features. Showing that the screen puts **raw punches next to the calculation** teaches users to *understand* the flag, not just clear it — leading to correct decisions and trustworthy data (G-01/G-05).

---

# 8. Task: Correct an Attendance Record (HR)

**Role required:** HR Officer.

Sometimes a record needs fixing — for example, someone forgot to check out.

1. Open the record (from the Exceptions worklist §7, or **Attendance → Records** → **Review**).
2. In the **Correction** area, choose the field to fix (e.g. *Last out*) and enter the correct value.
3. **Enter a reason** — this is **required**. Explain why (e.g. *"CCTV confirms left 17:02; forgot to punch."*).
4. Select **Save & Recalculate**.
5. The system recalculates hours and keeps a full record of the change.

**Important things to know:**
- ✅ The **original values are never lost** — corrections are added, not overwritten.
- ✅ Every correction records **who** made it, **when**, and **why** — visible in the record's **History**.
- ⚠ If someone else changed the record while you had it open, you'll be told to **reload** and try again, so no one's work is lost.
- 🔒 You cannot change the raw device check-ins themselves — only the calculated record. This keeps the original facts trustworthy.

**Decision — make the "reason required" and "originals preserved" rules explicit and reassuring.** Users are often nervous about "changing official records." Telling them plainly that **originals are kept, every change is attributed, and a reason is mandatory** (`05 §10.5`, BRULE-05) does two things: it reassures them the system is fair and auditable, and it sets the expectation that a reason is not optional — so the audit trail is always complete (G-05). The concurrency-reload note prevents the frustration of silently lost edits (`08 §7`).

---

# 9. Task: View Employees & Departments (HR)

**Role required:** HR Officer (Managers may view their team; some actions are admin-only → `13`).

1. Go to **People → Employees** (or **Departments**).
2. Use **search** and **filters** (e.g. by department) to find someone.
3. Select a person to see their details, status, and shift assignment.
4. To add or change core employee/department details, use the actions provided (subject to your permissions).

> Creating device enrolments, users, or roles is an **administrator** task — see `13_ADMIN_GUIDE.md`.

---

# 10. Task: Request Leave (Employee)

**Role required:** Employee (if self-service is enabled — otherwise your HR Officer submits on your behalf).

1. Go to **Leave → Requests**.
2. Select **New request**.
3. Choose the **leave type** (e.g. Annual, Sick), the **dates**, and add a **reason** if required.
4. Check your **remaining balance** shown on the form.
5. Select **Submit**.
6. Track the status: **Submitted → Approved / Rejected**. You'll be notified of the decision.

> If you don't have enough balance, the request may be blocked or need special approval.

---

# 11. Task: Approve or Reject Leave (Manager)

**Role required:** Department Manager (for your team) or HR Officer.

1. Go to **Leave → Approvals**. You'll see requests pending **for your team**.
2. Each row shows the employee, leave type, dates, and their **balance**.
3. Select **Approve** or **Reject**.
   - On **Reject**, enter a reason.
4. The employee is notified, and approved leave is automatically reflected in their attendance (they won't be marked absent for those days).

> If a request exceeds the employee's balance, approval is blocked unless your policy allows an override.

**Decision — tell the approver that approval flows straight into attendance.** Managers need to know that approving leave isn't just a form — it **automatically corrects the person's attendance** so approved days aren't counted as absent (FR-LV-005, BRULE-06). Making this consequence visible builds trust that the numbers will be right and discourages the old habit of chasing HR to "also fix the attendance."

---

# 12. Task: View Your Own Attendance (Employee)

**Role required:** Employee.

1. Go to **Attendance → My attendance** (or the Dashboard for a summary).
2. Choose a **date range**.
3. Review your daily records: check-in, check-out, hours, and any flags.
4. If something looks wrong, contact your **HR Officer** — they can review and correct it (§8).

> You can view your own records but cannot change them — corrections are made by HR so every change is properly recorded.

---

# 13. Task: See Your Team (Manager)

**Role required:** Department Manager.

1. Open the **Dashboard** (scoped to your team) or **Attendance → Records**.
2. Filter by **date** to see today's or a period's attendance.
3. Use the summary tiles to spot **absent** or **late** team members quickly.
4. Drill into a person's record for detail (read-only for managers).

> You see **your team only**. Requests for other teams' data are not permitted.

---

# 14. Task: Run Reports & Exports

**Role required:** HR Officer, Manager (own team), Auditor (read-only).

1. Go to **Reports**.
2. Choose a report (e.g. *Daily Attendance*, *Exceptions*, *Corrections*).
3. Set **filters**: department, employee, date range, shift.
4. Select **Run** to view on screen.
5. To download, select **Export** and choose the format (**CSV / Excel / PDF**).
6. The file downloads to your computer.

**Payroll export:** HR can produce the **payroll-ready export** (worked hours & overtime) for a period, in the agreed format, to hand to the payroll team.

> Reports only include data your role is allowed to see (e.g. a manager's reports cover their team). Exports are recorded for audit.

**Decision — state the two boundaries plainly: role-scope and audit.** Users must understand that (1) a report only ever contains data they're allowed to see (so a manager isn't alarmed that other teams are "missing"), and (2) exports are **logged**. Both come straight from the security design (`06 §5`, FR-RPT-006/007); surfacing them prevents confusion and sets the correct expectation that handling exported data is a tracked, responsible action.

---

# 15. Task: Review the Audit Trail (Auditor)

**Role required:** Auditor (or Administrator).

1. Go to **Admin → Audit Log** (visible to Auditors/Admins).
2. Filter by **entity** (e.g. an attendance record), **user**, or **date range**.
3. Review entries — each shows **who** did **what**, **when**, and the **before/after** values.

> The audit log is **read-only for everyone** — it cannot be edited or deleted from within TAMS. This is by design, so it can be trusted as evidence.

**Decision — reassure the auditor that the log is tamper-proof by design.** An auditor's confidence depends on knowing the evidence can't be altered. Stating plainly that the audit log is **read-only for everyone, by design** (`05 §10.8`, `06 §11`) tells the auditor the trail is trustworthy — which is the entire point of having it (G-05).

---

# 16. Notifications & Alerts

The **🔔 notifications** area (top bar) may alert you to things needing attention, such as:

- Open attendance exceptions (HR).
- Leave requests awaiting your approval (Managers).
- Leave decisions on your requests (Employees).

Select a notification to go straight to the relevant item.

> Device/technical alerts (e.g. a device offline) go to **administrators**, not everyday users — see `13`/`14`.

---

# 17. Accessibility Features

TAMS is designed to be usable by everyone (WCAG 2.1 AA, `08 §12`):

| Feature | How it helps |
|---|---|
| **Keyboard navigation** | You can use TAMS without a mouse (Tab to move, Enter to activate, Esc to close). |
| **Status labels, not just colour** | Every status has a word/icon, not only a colour — clear if you're colour-blind or printing. |
| **Screen-reader friendly** | Labels and messages are announced by screen readers. |
| **Clear focus** | The item you're on is clearly highlighted. |
| **Readable text & contrast** | Sufficient contrast and scalable text. |

> If you use assistive technology and hit a barrier, please report it so it can be fixed.

---

# 18. Frequently Asked Questions

| Question | Answer |
|---|---|
| **Why don't I see a menu mentioned in this guide?** | Your role doesn't include that task. This is normal and by design. |
| **My check-in/out is missing or wrong — what do I do?** | Contact your HR Officer; they can review and correct it (§8). You can't edit records yourself. |
| **Why was I marked absent when I was on leave?** | Leave must be **approved** to count. Check your request status (§10). |
| **Can I fix a record myself?** | No — corrections are made by HR so every change is recorded with a reason. |
| **Why does the login say "invalid credentials" without saying which is wrong?** | For security — it deliberately doesn't reveal which field failed. |
| **My account is locked.** | Too many failed attempts. Wait and retry, or contact your administrator. |
| **The dashboard number looks a moment old.** | It refreshes automatically and shows when it last updated. |
| **Where does my worked-hours data go for pay?** | HR produces a payroll export that goes to the payroll team; TAMS itself doesn't calculate salaries. |

**Decision — the FAQ answers the *why*, not just the *how*.** The most common user frustrations (missing menus, "absent" while on leave, can't self-edit, vague login errors) all stem from *deliberate* design choices (roles, approval flow, audit, security). Explaining the reason — not just the workaround — converts confusion into understanding and cuts repeat support questions, which is exactly what a good manual should do.

---

# 19. Troubleshooting (User-Level)

| Problem | Try this |
|---|---|
| Can't sign in | Recheck credentials; caps lock; wait if locked; else contact admin (`13`) |
| Page won't load / spinner stuck | Refresh; check your internet; try again shortly |
| "You don't have permission" | The task isn't in your role; ask your admin if you need access |
| "This record changed — reload" | Someone edited it meanwhile; reload and reapply your change (§8) |
| Something looks broken | Note the **reference code** shown on the error and give it to support — it helps them find the exact problem fast |
| Data looks wrong | HR: correct it (§8). Others: report to HR |

> Anything beyond these (system down, device problems, slow for everyone) is for **administrators/IT** — see `13`/`14`.

**Decision — teach users to capture the error reference code.** When something genuinely breaks, the on-screen **reference (correlation) code** (`05 §6`, `06 §11`) lets support find the exact log entry instantly. Teaching users to quote it turns a vague "it's broken" report into a one-lookup fix — the user-facing payoff of the correlation-id design threaded through every earlier document.

---

# 20. Glossary

Everyday-language versions of key terms (full technical definitions are in earlier documents).

| Term | Plain meaning |
|---|---|
| **Punch** | A check-in or check-out at a device. |
| **Shift** | Your expected working hours. |
| **Attendance record** | Your daily attendance summary. |
| **Exception** | A day flagged for a person to review. |
| **Correction** | A tracked fix to a record (needs a reason). |
| **Leave balance** | How much leave you have left. |
| **Role** | What you're allowed to see and do. |
| **Dashboard** | Your at-a-glance home screen. |
| **Export** | Downloading data as a file (CSV/Excel/PDF). |
| **Audit log** | The permanent record of who changed what. |
| **Reference code** | The code shown on errors, for support. |

---

# 21. Documentation Review Checklist

**Reviewer instructions:** mark ✅ Pass / ⚠️ Needs change / ❌ Fail. Approved when all **Mandatory** items pass.

### 21.1 Completeness

| # | Check | Mandatory | Status |
|---|---|---|---|
| C-01 | Audience & role guidance clear | ✔ | ☐ |
| C-02 | Key concepts explained in plain language | ✔ | ☐ |
| C-03 | Login/getting started covered | ✔ | ☐ |
| C-04 | Navigation explained | ✔ | ☐ |
| C-05 | Dashboard explained | ✔ | ☐ |
| C-06 | Statuses explained | ✔ | ☐ |
| C-07 | Core HR tasks (exceptions, corrections) covered | ✔ | ☐ |
| C-08 | Leave request & approval covered | ✔ | ☐ |
| C-09 | Employee self-view & manager team-view covered | ✔ | ☐ |
| C-10 | Reports & exports covered | ✔ | ☐ |
| C-11 | Audit review (Auditor) covered | ✔ | ☐ |
| C-12 | Notifications covered | ✔ | ☐ |
| C-13 | Accessibility features covered | ✔ | ☐ |
| C-14 | FAQ & troubleshooting included | ✔ | ☐ |
| C-15 | Glossary included | ✔ | ☐ |

### 21.2 Quality & Soundness

| # | Check | Mandatory | Status |
|---|---|---|---|
| Q-01 | Task-oriented & role-grouped (not menu-dump) | ✔ | ☐ |
| Q-02 | Plain, non-technical language | ✔ | ☐ |
| Q-03 | Each task states role required & clear steps | ✔ | ☐ |
| Q-04 | Explains *why* for common confusions (roles, audit, security) | ✔ | ☐ |
| Q-05 | Reinforces trust model (originals kept, reason required) | ✔ | ☐ |
| Q-06 | Accessibility explained for users | ✔ | ☐ |
| Q-07 | Every significant writing decision explained | ✔ | ☐ |

### 21.3 Alignment

| # | Check | Mandatory | Status |
|---|---|---|---|
| A-01 | Matches UI/UX design (`08`) | ✔ | ☐ |
| A-02 | Consistent with security/role model (`06`) | ✔ | ☐ |
| A-03 | Reflects correction/audit behaviour (`05`) | ✔ | ☐ |
| A-04 | Defers admin tasks to `13`, ops to `14` | ✔ | ☐ |
| A-05 | Self-service leave conditional on scope (OQ-07) | ✔ | ☐ |

### 21.4 Governance

| # | Check | Mandatory | Status |
|---|---|---|---|
| G-01 | Document control & versioning present | ✔ | ☐ |
| G-02 | Approval sign-off present | ✔ | ☐ |
| G-03 | Ready to proceed to `13_ADMIN_GUIDE.md` on approval | ✔ | ☐ |

---

### ✅ Approval Gate

> **This User Manual (v1.0) is submitted for your approval.** I will **not** begin `13_ADMIN_GUIDE.md` until you approve or request changes.

**Please respond with one of:**
1. **Approved** → I proceed to `13_ADMIN_GUIDE.md`.
2. **Approved with changes** → list changes; I revise then proceed.
3. **Changes required** → list changes; I revise and resubmit this document only.

*End of Document — TAMS-USER-012 v1.0*
