#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Controlled DB migration job (11 §6): backup -> migrate -> verify. Production
# apps do NOT auto-migrate (enforced in Program.cs); this is the deliberate,
# gated, logged step that changes the schema, run BEFORE deploying the app
# version that expects it (expand -> migrate -> contract for zero downtime).
#
# Required env:
#   TAMS_DB_CONNECTION   ADO.NET connection string for the migration principal
#                        (needs DDL rights — distinct from the least-privilege
#                        app principal, 06 §5).
# Optional env:
#   EFBUNDLE             path to the efbundle produced by build-bundle.sh
#                        (default: ./artifacts/migrations/efbundle)
#   SKIP_BACKUP          set to 1 only in non-prod; prod MUST back up first (§10).
#
# The DB backup itself is environment-specific (SQL Agent job, native BACKUP,
# or platform snapshot); wire your backup command into take_backup() below.
# ---------------------------------------------------------------------------
set -euo pipefail

EFBUNDLE="${EFBUNDLE:-./artifacts/migrations/efbundle}"
: "${TAMS_DB_CONNECTION:?TAMS_DB_CONNECTION must be set (migration principal, DDL rights)}"

take_backup() {
  if [[ "${SKIP_BACKUP:-0}" == "1" ]]; then
    echo "WARNING: skipping DB backup (SKIP_BACKUP=1). Never do this in production."
    return 0
  fi
  echo ">> Taking verified DB backup (§10)..."
  # TODO(ops): invoke your environment's encrypted backup + restore-verify here.
  # This step MUST succeed before migrating; a migration without a good backup
  # has no safe rollback. Fail closed if the backup command is not wired.
  echo "ERROR: backup command not configured. Wire take_backup() or set SKIP_BACKUP=1 (non-prod only)." >&2
  return 1
}

echo "== TAMS migration job =="
take_backup

if [[ ! -x "$EFBUNDLE" ]]; then
  echo "ERROR: migrations bundle not found/executable at '$EFBUNDLE'. Run build-bundle.sh." >&2
  exit 1
fi

echo ">> Applying migrations..."
"$EFBUNDLE" --connection "$TAMS_DB_CONNECTION"

echo ">> Migration applied. Post-migration verification (smoke) runs next in the pipeline (§6)."
echo "== Done. Now deploy the compatible app version. =="
