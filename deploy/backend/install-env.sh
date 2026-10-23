#!/usr/bin/env bash
# Installs a new /etc/strategy-trading-lab/api.env on the server, from the file deploy-env.ps1 uploads:
#   sudo bash install-env.sh <uploaded env file>
# Checks it, keeps the old one as api.env.previous, restarts the API and checks /health. If the API isn't healthy
# with the new file, it puts the old one back. Prints variable names only, never values.
set -euo pipefail

uploaded="$1"
env_file=/etc/strategy-trading-lab/api.env
service=strategy-trading-lab-api
required=(DATABASE_URL DATABASE_URL_UNPOOLED)

# The upload holds secrets: gone however this ends.
trap 'shred -u "$uploaded" 2>/dev/null || rm -f "$uploaded"' EXIT

# Same lock as install.sh: never during a release install.
exec 9> /run/lock/strategy-trading-lab-api.lock
flock 9

# Written on Windows, it may have CRLF line ends: systemd would keep the \r in every value.
new="$(mktemp)"
trap 'shred -u "$uploaded" "$new" 2>/dev/null || rm -f "$uploaded" "$new"' EXIT
tr -d '\r' < "$uploaded" > "$new"

# One KEY=value per line (no quotes, no spaces around =), comments and blank lines allowed.
if bad="$(grep -nvE '^([A-Z][A-Z0-9_]*=.*|#.*|[[:space:]]*)$' "$new")"; then
  echo "Not KEY=value on line(s): $(cut -d: -f1 <<< "$bad" | paste -sd, -)" >&2
  exit 1
fi
keys() { grep -oE '^[A-Z][A-Z0-9_]*' "$1" | sort -u; }
for key in "${required[@]}"; do
  grep -qE "^$key=.+" "$new" || { echo "$key is missing or empty." >&2; exit 1; }
done
grep -qE '^ADMIN_PASSWORD=.+' "$new" || echo "Warning: no ADMIN_PASSWORD, so the admin page can't switch the database." >&2

install -d -m 755 /etc/strategy-trading-lab
if [ -f "$env_file" ] && cmp -s "$new" "$env_file"; then
  echo "$env_file is unchanged: nothing to do."
  exit 0
fi

# What changes, by name.
if [ -f "$env_file" ]; then
  added="$(comm -13 <(keys "$env_file") <(keys "$new") | paste -sd' ' -)"
  removed="$(comm -23 <(keys "$env_file") <(keys "$new") | paste -sd' ' -)"
  changed="$(comm -12 <(keys "$env_file") <(keys "$new") | while read -r key; do
    [ "$(grep -E "^$key=" "$env_file")" = "$(grep -E "^$key=" "$new")" ] || echo "$key"; done | paste -sd' ' -)"
  echo "Added: ${added:-none}. Removed: ${removed:-none}. Changed: ${changed:-none}."
  install -m 600 -o root -g root "$env_file" "$env_file.previous"
fi
install -m 600 -o root -g root "$new" "$env_file"

systemctl restart "$service"
for _ in $(seq 30); do
  if curl -fs http://127.0.0.1:5080/health > /dev/null; then
    echo "Installed: the API is healthy on $(curl -fsS http://127.0.0.1:5080/version)"
    exit 0
  fi
  sleep 2
done

echo "The API isn't healthy after 60s with the new $env_file:" >&2
journalctl -u "$service" -n 30 --no-pager >&2
if [ -f "$env_file.previous" ]; then
  echo "Putting the previous one back." >&2
  install -m 600 -o root -g root "$env_file.previous" "$env_file"
  systemctl restart "$service"
fi
exit 1
