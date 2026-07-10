# TAMS — Go-Live Guide (Plain English)

**Who this is for:** the person setting up the Time & Attendance system for the first
time. You do **not** need to be a programmer. Follow the steps in order. Each step
tells you *what you need*, *what to do*, and *how to know it worked*.

> **First, the big picture — this is important.**
> TAMS is a **website that runs on your own server**, not an app you install on each
> person's computer. Staff and HR just open a **web browser** (Chrome, Edge) and go to
> a web address — nothing to install on their PCs. What *you* set up is the **server
> side**: one computer that runs the system, plus the ZKTeco fingerprint/face devices.
>
> There are three pieces running on that server, all started together with one command:
> 1. **The API** — the brain (rules, data).
> 2. **The Worker** — talks to the ZKTeco devices and collects punches.
> 3. **The Website (SPA)** — what people see in the browser.
> You don't manage these individually; the setup starts all three for you.

---

## What you need before you start (checklist)

- [ ] **A server computer** — a Windows or Linux machine that stays on 24/7 (a proper
      server, or at minimum a dedicated PC). This runs everything.
- [ ] **Docker installed** on that server (Docker Desktop on Windows, or Docker Engine
      on Linux). This is the tool that runs the three pieces. *(Your IT person or the
      development team installs this once.)*
- [ ] **The ZKTeco attendance devices** mounted at your doors/entry points, powered on,
      and connected to the same network as the server.
- [ ] **A web address (domain)** for staff to use, e.g. `tams.yourcompany.com`, pointing
      to the server — and an **SSL/TLS certificate** for it (the padlock in the browser).
      *(Your IT/hosting provider arranges this.)*
- [ ] **The TAMS software package** from the development team — this whole project folder,
      or the pre-built images.
- [ ] **Two passwords/secrets you'll create** in Step 2 (a database password and a
      security key). Keep them safe — treat them like the office safe combination.

If any box is unchecked, sort that out first. The system cannot go live without them.

---

## Step 1 — Put the software on the server

**What to do:** Copy the TAMS project folder onto the server (or have the dev team clone
it). Open a terminal/command window **in that folder**.

**How you know it worked:** You can see the `deploy` folder inside it (it contains the
setup files this guide refers to).

---

## Step 2 — Fill in your settings (the one file you edit)

All your private settings live in **one file**. There's a ready-made template.

**What to do:**
1. Find `deploy/.env.example`.
2. Make a copy of it named `deploy/.env` (same folder).
3. Open `deploy/.env` in Notepad (or any text editor) and fill in these values:

| Setting | What to put | Plain meaning |
|---|---|---|
| `MSSQL_SA_PASSWORD` | A strong password you invent | The database's master password |
| `TAMS_DB_CONNECTION` | *(dev team fills this)* | How the app finds the database |
| `TAMS_JWT_SIGNING_KEY` | A long random secret (**32+ characters**) | The system's "signature" for logins |
| `TAMS_SPA_ORIGIN` | e.g. `https://tams.yourcompany.com` | Your web address |
| `TAMS_ALLOWED_HOSTS` | e.g. `tams.yourcompany.com` | The web address again (security) |
| `TAMS_TLS_CERT_DIR` | Folder holding your certificate files | Your padlock/SSL files |

> **Tip for the random security key:** on the server run `openssl rand -base64 48` and
> paste the result. If it's shorter than 32 characters, **the system will refuse to
> start on purpose** — that's a safety feature, not a bug.

**How you know it worked:** `deploy/.env` exists and every value above is filled in with
real values (no `CHANGE_ME` left).

> **Never email or share the `.env` file.** It holds your secrets. It is deliberately
> excluded from the code repository.

---

## Step 3 — Put your SSL certificate in place

**What to do:** Put your certificate file and key file into the folder you named in
`TAMS_TLS_CERT_DIR`, named exactly:
- `tams.crt` (the certificate)
- `tams.key` (the private key)

*(Your IT/hosting provider gives you these two files when they issue the certificate.)*

**How you know it worked:** Both files are in that folder with those exact names.

---

## Step 4 — Set up the database (one-time)

The system keeps a strict rule: **it never changes the database by itself.** You run a
controlled step once to create the tables. This protects your data.

**What to do:** From the project folder, run the migration job. The dev team will give
you the exact command, but it is essentially:
1. **Back up first** (if any data already exists — for a brand-new install there's nothing yet).
2. Run `deploy/migrations/migrate.sh` (it applies the database structure).

**How you know it worked:** The command finishes with a success message and no errors.
The database now has all the tables (Employees, Attendance, Devices, Leave, etc.).

---

## Step 5 — Start the system

This is the moment it comes alive. One command starts all three pieces.

**What to do:** From the project folder, run:

```
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d --build
```

The first time, this takes several minutes (it's building everything). After that,
starts are quick.

**How you know it worked:** Run `docker compose -f deploy/docker-compose.yml ps` — you
should see the pieces (**proxy, api ×2, worker, db**) listed as **running / healthy**.

---

## Step 6 — Check it's healthy

**What to do:** In a browser on the server, go to:
```
https://tams.yourcompany.com/health/ready
```

**How you know it worked:** You see a message like `{"status":"ready","database":"ok"}`.
That means the app is up **and** talking to the database. (If it says "not-ready", the
database connection is wrong — recheck Step 2.)

---

## Step 7 — Log in and change the admin password

**What to do:**
1. Go to `https://tams.yourcompany.com` in a browser.
2. Log in with the starter admin account the dev team set up.
3. **Immediately change the admin password** to your own.

**How you know it worked:** You see the Dashboard, and your new password works on the
next login.

> The system is built so no weak default password survives into real use — changing it
> now is part of go-live, not optional.

---

## Step 8 — Add your real data

Now make it *yours*. In the web app:
1. **Departments** — add your company's departments.
2. **Employees** — add staff (or import if the dev team set up an import).
3. **Shifts** — define working hours, breaks, grace periods, overtime rules.
4. **Assign shifts** to employees/departments.
5. **Leave types** — set up annual leave etc. and entitlements.

**How you know it worked:** You can see your employees and shifts listed in the app.

---

## Step 9 — Connect and enrol the attendance devices

**What to do:**
1. In the app, go to **Devices** and **register** each ZKTeco device (name, IP address).
2. **Enrol employees** on the devices — link each person to their device user ID
   (and their fingerprint/face is captured on the physical device as usual).

**How you know it worked:** The Devices screen shows each device as reachable / recently
seen, and enrolled employees appear against it.

> Behind the scenes the **Worker** now polls these devices every 30 seconds and pulls in
> punches automatically — no manual step needed.

---

## Step 10 — The most important test: a real punch

Health checks tell you the system *started*. This tells you it actually **does its job**.

**What to do:**
1. Have someone **punch in** on a real device (fingerprint/face/card).
2. Wait up to a minute.
3. In the app, open **Attendance** and find that punch turned into a record.

**How you know it worked:** The punch shows up as an attendance record for the right
person, on the right day. **This is the sign you are truly live.**

---

## Step 11 — Go live

- [ ] Tell staff the web address and how to log in.
- [ ] Confirm HR can see dashboards, run reports, and export.
- [ ] Make sure **backups** are scheduled (Step 4's backup, but automatic and regular).
- [ ] Keep an eye on it for the first week or two (this is called *hypercare*).

**You're live.** People punch on the devices; the system collects, calculates, and
reports; HR manages leave and exports payroll data.

---

## If something goes wrong

| Symptom | Likely cause | What to do |
|---|---|---|
| App won't start | Missing/short security key or DB password | Recheck `deploy/.env` (Step 2); the key must be 32+ chars |
| `/health/ready` says "not-ready" | Database not reachable / not migrated | Recheck DB connection (Step 2) and that Step 4 ran |
| Can't open the website | Certificate or domain not set up | Recheck Steps 3 and the domain pointing to the server |
| Punches not appearing | Device offline or not enrolled | Check the device is on-network and the employee is enrolled (Step 9) |
| Need to undo a bad update | — | Restart the previous version; restore the backup taken in Step 4 |

**Rollback rule of thumb:** if a new version misbehaves, you go back to the previous one
and restore the backup. Always take a backup before any change.

---

## Two things to plan for (ask the dev team)

1. **Real ZKTeco devices vs. the test simulator.** The system ships with a *simulator*
   so it can be demonstrated without hardware. To collect punches from your **actual**
   ZKTeco devices, the dev team switches on the real device connection (a small, planned
   configuration change — no rewrite). **Confirm this is done before you rely on live capture.**
2. **Who supports it day-to-day?** Decide who patches the server, watches the alerts, and
   handles issues — your IT team or a maintenance contract with the developers.

---

*Companion documents: `deploy/RUNBOOK.md` (technical go-live checklist for engineers),
and `docs/12_USER_MANUAL.md` / `docs/13_ADMIN_GUIDE.md` (day-to-day use).*
