#!/usr/bin/env bash
# Installs one backend release on the server, from the unpacked package deploy.ps1 uploads:
#   sudo bash install.sh <version>
# Applies the migrations, switches /opt/strategy-trading-lab-api/current to the new release, restarts the
# service and checks /health. If the new release isn't healthy it switches back to the previous one.
set -euo pipefail

version="$1"
package="$(cd "$(dirname "$0")" && pwd)"
root=/opt/strategy-trading-lab-api
env_file=/etc/strategy-trading-lab/api.env
service=strategy-trading-lab-api
release="$root/releases/${version}_$(date -u +%Y%m%dT%H%M%SZ)"

if [ ! -f "$env_file" ]; then
  echo "$env_file is missing: set it up as deploy/backend/README.md says." >&2
  exit 1
fi

id stlab-api &>/dev/null || useradd --system --no-create-home --shell /usr/sbin/nologin stlab-api
install -d "$root/releases"
cp -r "$package/app" "$release"
cp "$package/efbundle" "$release/efbundle"
# Packages built on Windows lose the executable bit.
chmod 755 "$release/StrategyTradingLab.Api" "$release/efbundle"

echo "Applying migrations"
# Same user and environment file as the service; the bundle connects with DATABASE_URL_UNPOOLED.
# It's a single-file app that unpacks itself first: stlab-api has no home, so into a private /tmp.
systemd-run --quiet --wait --pipe --collect --property=User=stlab-api \
  --property=EnvironmentFile="$env_file" --property=PrivateTmp=yes \
  --setenv=DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp --working-directory="$release" "$release/efbundle"

previous="$(readlink -f "$root/current" || true)"
ln -sfn "$release" "$root/current"
install -m 644 "$package/$service.service" "/etc/systemd/system/$service.service"
systemctl daemon-reload
systemctl enable --quiet "$service"
systemctl restart "$service"

for _ in $(seq 30); do
  if curl -fs http://127.0.0.1:5080/health; then
    echo
    echo "Installed $version: $(curl -fsS http://127.0.0.1:5080/version)"
    # Keep the last 5 releases for switching back by hand.
    ls -1dt "$root"/releases/*/ | tail -n +6 | xargs -r rm -rf
    exit 0
  fi
  sleep 2
done

echo "$version isn't healthy after 60s:" >&2
journalctl -u "$service" -n 50 --no-pager >&2
if [ -n "$previous" ] && [ -d "$previous" ] && [ "$previous" != "$release" ]; then
  echo "Switching back to $previous" >&2
  ln -sfn "$previous" "$root/current"
  systemctl restart "$service"
fi
exit 1
