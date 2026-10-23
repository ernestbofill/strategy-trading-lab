# Backend deploy

The API runs on the Oracle server (Ubuntu 24.04, arm64) as the systemd service `strategy-trading-lab-api`, on `127.0.0.1:5080`. nginx serves it at `https://140-238-209-178.sslip.io/api/` (`/api/health`, `/api/version`).

- **On merge to `main`**: the [Deploy backend](../../.github/workflows/deploy-backend.yml) workflow runs when `backend/` (not its tests or Markdown) or `deploy/backend/` changes, after building and testing it, then tags the commit `backend-v<version>`. It can also be run by hand from the Actions tab.
- **From VS Code**: *Terminal → Run Task → Deploy backend to Oracle*. It deploys your working copy, so use it for hotfixes and checks, not instead of a PR. It doesn't tag.

Both run [deploy.ps1](deploy.ps1): it publishes the API self-contained for `linux-arm64` (the server has no .NET), builds the EF migrations bundle, uploads both and runs [install.sh](install.sh) on the server. That applies the migrations, installs the release under `/opt/strategy-trading-lab-api/releases/`, points `current` at it, restarts the service and waits for `/health`. If the new release isn't healthy it switches back to the previous one (the migrations stay applied). The last 5 releases are kept.

## Versions

The version is `<Version>` in [backend/Directory.Build.props](../../backend/Directory.Build.props). It follows the frontend's scheme: minor for a new feature or endpoint (`0.1.0-alpha` → `0.2.0-alpha`), patch for a fix or small tweak (`0.2.0-alpha` → `0.2.1-alpha`), keeping `-alpha`. Every PR that changes the backend (not only its tests or Markdown) bumps it; CI fails if it doesn't, and the workflow refuses to deploy a version already tagged on another commit.

`GET /api/version` is for the client to show which API it talks to:

```json
{ "version": "0.1.0-alpha", "commit": "3f9c2e1…", "builtAt": "2026-10-18T12:04:31+00:00", "database": "production" }
```

`database` is the one the API uses: production unless switched to dev on the admin page (`/admin`), which needs `DATABASE_URL_DEV` and `ADMIN_PASSWORD` in `api.env`. The choice is in `/var/lib/strategy-trading-lab-api/database.json`; the deploy's migrations always go to production.

`builtAt` is when the release was built, minutes before it went live. It's `null` in Debug builds.

## One-time server setup

1. The API's environment, `/etc/strategy-trading-lab/api.env` (readable by root only): see *The environment file* below.

2. The `/api/` location in nginx: copy [deploy/nginx/strategy-trading-lab.conf](../nginx/strategy-trading-lab.conf) to the server as its header says.

install.sh creates the rest (the `stlab-api` user, `/opt/strategy-trading-lab-api`, the systemd unit) on every deploy.

## The environment file

`/etc/strategy-trading-lab/api.env` holds the API's secrets: the production Neon branch's variables (`DATABASE_URL`, `DATABASE_URL_UNPOOLED`, `AWS_*`, as `neon env pull` writes them for the `production` branch), `DATABASE_URL_DEV` (the dev branch's pooled URL, so the admin page can switch to dev) and `ADMIN_PASSWORD`. [production.env.example](production.env.example) lists them.

Keep the values in `deploy/backend/production.env` on your machine (gitignored, never committed; copy the example) and send them with [deploy-env.sh](deploy-env.sh) (Mac, Linux) or [deploy-env.ps1](deploy-env.ps1) (Windows), or *Terminal → Run Task → Deploy backend environment to Oracle*. It checks the file (one `KEY=value` per line, the two database URLs present), uploads it, and [install-env.sh](install-env.sh) installs it on the server: root-only (`600`), the old one kept as `api.env.previous`, the API restarted and checked on `/health`, and the old file put back if it isn't healthy. It prints variable names only, never values. A file with Windows line ends is fine.

The database URLs are set only here, never on the admin page: the page switches between `DATABASE_URL` (production) and `DATABASE_URL_DEV`, so knowing its password can't point the API at another database.

## On the server

```bash
systemctl status strategy-trading-lab-api
```

```bash
journalctl -u strategy-trading-lab-api -f
```

To switch back to an older release by hand: `sudo ln -sfn /opt/strategy-trading-lab-api/releases/<release> /opt/strategy-trading-lab-api/current && sudo systemctl restart strategy-trading-lab-api`.
