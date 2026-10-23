#!/usr/bin/env bash
# Uploads the API's environment file to the Oracle server as /etc/strategy-trading-lab/api.env and restarts the API.
# The bash twin of deploy-env.ps1, for the Mac and Linux. The values live in deploy/backend/production.env by default
# (*.env is gitignored), made from production.env.example. The server keeps the old file as api.env.previous and puts
# it back if the API isn't healthy with the new one. Prints variable names only, never values. Needs the strategy-lab
# host in ~/.ssh/config.
# Usage: deploy-env.sh [env-file] [ssh-host]
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
env_file=${1:-$here/production.env}
ssh_host=${2:-strategy-lab}

[ -f "$env_file" ] || { echo "$env_file doesn't exist: copy production.env.example next to it and fill in the values." >&2; exit 1; }

# The same checks as the server's, before anything is sent (Windows line ends are fine).
lines=$(tr -d '\r' < "$env_file")
bad=$(printf '%s\n' "$lines" | grep -nvE '^([A-Z][A-Z0-9_]*=.*|#.*|[[:space:]]*)$' | cut -d: -f1 | paste -sd, - || true)
[ -z "$bad" ] || { echo "Not KEY=value on line(s) $bad of $env_file" >&2; exit 1; }
for key in DATABASE_URL DATABASE_URL_UNPOOLED; do
  printf '%s\n' "$lines" | grep -qE "^$key=.+" || { echo "$key is missing or empty in $env_file" >&2; exit 1; }
done
keys=$(printf '%s\n' "$lines" | grep -oE '^[A-Z][A-Z0-9_]*=' | tr -d '=')
known=$(grep -oE '^#?[[:space:]]*[A-Z][A-Z0-9_]*=' "$here/production.env.example" | tr -d '#= ')
unknown=$(printf '%s\n' "$keys" | grep -vxF -f <(printf '%s\n' "$known") | paste -sd' ' - || true)
[ -z "$unknown" ] || echo "Warning: not in production.env.example (a typo?): $unknown" >&2
echo "Uploading $(printf '%s\n' "$keys" | wc -l | tr -d ' ') variables to $ssh_host: $(printf '%s\n' "$keys" | paste -sd' ' -)"

# A private folder per run; install-env.sh deletes the upload whatever happens.
remote=$(ssh "$ssh_host" 'mktemp -d /tmp/strategy-trading-lab-env.XXXXXX') || { echo "Can't reach $ssh_host" >&2; exit 1; }
trap 'ssh "$ssh_host" "rm -rf $remote"' EXIT
scp -q "$env_file" "$ssh_host:$remote/api.env"
scp -q "$here/install-env.sh" "$ssh_host:$remote/install-env.sh"
ssh "$ssh_host" "sudo bash $remote/install-env.sh $remote/api.env"

echo "Live: $(curl -fsS https://140-238-209-178.sslip.io/api/health)"
