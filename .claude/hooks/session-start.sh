#!/usr/bin/env bash
# Prepares a Claude Code cloud session: installs the frontend's packages and the backend's
# .NET tools. Local machines are set up by hand, so it does nothing there.
set -u

[ "${CLAUDE_CODE_REMOTE:-}" = "true" ] || exit 0
cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0

if command -v npm >/dev/null 2>&1; then
  [ -d frontend/node_modules ] || npm ci --prefix frontend --no-audit --no-fund >/dev/null 2>&1 \
    || echo "session-start: npm ci failed in frontend/"
else
  echo "session-start: npm is missing; add Node 24 to the cloud environment's setup."
fi

if command -v dotnet >/dev/null 2>&1; then
  (cd backend && dotnet tool restore >/dev/null 2>&1) || echo "session-start: dotnet tool restore failed in backend/"
else
  echo "session-start: dotnet is missing; add .NET 10 to the cloud environment's setup to build the backend."
fi

# The dev database follows the migrations on this branch (port 5432 is blocked here, so over HTTPS).
if [ -n "${DATABASE_URL:-}${DATABASE_URL_UNPOOLED:-}" ] && command -v python3 >/dev/null 2>&1; then
  python3 -I backend/scripts/migrate-dev-https.py >/dev/null 2>&1 || echo "session-start: migrating the dev database failed; run python3 backend/scripts/migrate-dev-https.py to see why."
fi

[ -f .env.local ] || [ -n "${DATABASE_URL:-}" ] \
  || echo "session-start: no Neon variables; [NeonFact] tests skip and the API can't reach a database."
exit 0
