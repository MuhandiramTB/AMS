# TAMS Deployment Runbook (Go-Live)

Operationalises **11_DEPLOYMENT.md §14** against the concrete artifacts in this
`deploy/` directory. A repeatable, auditable checklist. Day-2 operations live in
`docs/14_MAINTENANCE_GUIDE.md`.

> **Golden rule:** build the images **once**, promote the *same* tag Dev → Test →
> Staging → Prod. Never rebuild for Prod. Secrets come only from the environment
> (`deploy/.env` / secret store), never from source or images.

---

## Artifacts produced by Phase 7

| Artifact | Path | Purpose |
|---|---|---|
| API image | `src/TAMS.Api/Dockerfile` | Stateless API (≥2 replicas) |
| Worker image | `src/TAMS.Worker/Dockerfile` | ZKTeco capture (single active) |
| SPA + proxy image | `client/Dockerfile` + `deploy/nginx/nginx.conf` | Static SPA, TLS, headers, `/api` proxy |
| Compose topology | `deploy/docker-compose.yml` | On-prem baseline (§7) |
| Migrations bundle | `deploy/migrations/build-bundle.sh` | Self-contained schema updater |
| Migrate job | `deploy/migrations/migrate.sh` | Gated backup→migrate→verify (§6) |
| Config template | `deploy/.env.example` | Per-env values (no secrets) |
| CI/CD pipeline | `.github/workflows/ci.yml` | Quality + security gates (§4) |

---

## 1. Pre-deployment (11 §14.1)

- [ ] All **P6 gates** passed on the release candidate (perf / security / a11y / UAT) — `10 §15`
- [ ] Change request approved & scheduled
- [ ] Target image tag identified — the **Staging-validated** one (`TAMS_TAG`), not a fresh build
- [ ] `deploy/.env` populated from the **secret store**; `JWT_SIGNING_KEY` ≥ 32 bytes (app fails fast otherwise)
- [ ] Least-privilege **app DB principal** in the connection string (not `sa`, not the migration principal)
- [ ] DB backup taken **and restore-verified** (§10)
- [ ] Rollback plan confirmed (previous image tag available)
- [ ] Device VLAN / firewall to the ZKTeco fleet confirmed reachable from the Worker
- [ ] TLS cert + key present in `TAMS_TLS_CERT_DIR` (`tams.crt`, `tams.key`); security headers/CORS reviewed (§11)
- [ ] Monitoring / alerting live: device-unreachable, auth-failure spikes, failed migrations (§9)

## 2. Deployment (11 §14.2)

- [ ] **Migrate first** — run the gated job (backup → migrate → verify):
      `EFBUNDLE=… TAMS_DB_CONNECTION=<migration-principal> deploy/migrations/migrate.sh`
- [ ] Deploy API (rolling — replicas replaced one at a time behind the proxy)
- [ ] Deploy Worker (stop/start; **resumes from its per-device watermark** — no lost/dup punches)
- [ ] Serve the SPA build (the proxy image)
      `docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d`
- [ ] Confirm the deploy-time hardening checklist (§11): TLS redirect + HSTS, headers, CORS allow-list,
      Swagger disabled in Prod, secrets only from store, DB TDE + audit grants, device VLAN segmented

## 3. Post-deployment (11 §14.3)

- [ ] `GET /health/ready` green (DB + worker/device subsystem) — `05 §10.9`
- [ ] Smoke: login → load dashboard → device sync → create record → export
- [ ] **Prove the core path:** a real punch flows device → attendance record (lab/known device)
- [ ] Verify audit entries were produced for the above
- [ ] Logs + metrics flowing; no error spike
- [ ] Sign-off; enter hypercare (`09 §10`, `14`)

## 4. Rollback (if triggered) (11 §14.4)

- [ ] Re-deploy the previous known-good image tag (`TAMS_TAG`)
- [ ] Restore / forward-fix the DB if a migration was involved (§6)
- [ ] Re-run smoke tests; confirm health
- [ ] Incident note with correlation ids for analysis (`06 §16`)

---

**Go-live is a signed checklist ending in a _proven punch flow_ — not merely green
health checks.** For an attendance system, "the API is up" is not "capture works."
