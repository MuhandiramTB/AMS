#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Build a self-contained EF Core migrations bundle for controlled deployment
# (11 §6). The bundle is a single executable that applies migrations without
# the SDK or source on the target — it is produced ONCE from the same commit
# as the app images and promoted alongside them (DP-02).
#
# Usage:  deploy/migrations/build-bundle.sh [output-dir]
# Output: <output-dir>/efbundle(.exe)  — run it as the gated migration job (§6).
# ---------------------------------------------------------------------------
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT_DIR="${1:-$REPO_ROOT/artifacts/migrations}"
STARTUP="src/TAMS.Api/TAMS.Api.csproj"
PROJECT="src/TAMS.Infrastructure/TAMS.Infrastructure.csproj"

cd "$REPO_ROOT"
mkdir -p "$OUT_DIR"

# dotnet-ef is a local/global tool; install on demand if absent.
if ! dotnet ef --version >/dev/null 2>&1; then
  echo "Installing dotnet-ef tool..."
  dotnet tool install --global dotnet-ef >/dev/null
  export PATH="$PATH:$HOME/.dotnet/tools"
fi

echo "Building EF migrations bundle -> $OUT_DIR/efbundle"
dotnet ef migrations bundle \
  --configuration Release \
  --project "$PROJECT" \
  --startup-project "$STARTUP" \
  --output "$OUT_DIR/efbundle" \
  --force

echo "Done. Apply with the migrate job (deploy/migrations/migrate.sh) — backup first."
