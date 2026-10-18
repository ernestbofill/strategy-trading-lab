# Backend deploy

The API runs on the Oracle server (Ubuntu 24.04, arm64) as the systemd service `strategy-trading-lab-api`, on `127.0.0.1:5080`. nginx serves it at `https://140-238-209-178.sslip.io/api/` (`/api/health`, `/api/version`).

- **On merge to `main`**: the [Deploy backend](../../.github/workflows/deploy-backend.yml) workflow runs when `backend/` (not its tests or Markdown) or `deploy/backend/` changes, after building and testing it, then tags the commit `backend-v<version>`. It can also be run by hand from the Actions tab.
- **From VS Code**: *Terminal → Run Task → Deploy backend to Oracle*. It deploys your working copy, so use it for hotfixes and checks, not instead of a PR. It doesn't tag.

Both run [deploy.ps1](deploy.ps1): it publishes the API self-contained for `linux-arm64` (the server has no .NET), builds the EF migrations bundle, uploads both and runs [install.sh](install.sh) on the server. That applies the migrations, installs the release under `/opt/strategy-trading-lab-api/releases/`, points `current` at it, restarts the service and waits for `/health`. If the new release isn't healthy it switches back to the previous one (the migrations stay applied). The last 5 releases are kept.

## Versions

The version is `<Version>` in [backend/Directory.Build.props](../../backend/Directory.Build.props). We're in alpha: `0.1.0-alpha.N`. Every PR that changes the backend (not only its tests or Markdown) bumps N by one; CI fails if it doesn't, and the workflow refuses to deploy a version already tagged on another commit.

`GET /api/version` is for the client to show which API it talks to:

```json
{ "version": "0.1.0-alpha.2", "commit": "3f9c2e1…", "builtAt": "2026-10-18T12:04:31+00:00" }
```

`builtAt` is when the release was built, minutes before it went live. It's `null` in Debug builds.

## One-time server setup

1. The production Neon branch's variables, readable by root only:

   ```bash
   sudo install -d -m 755 /etc/strategy-trading-lab
   sudo install -m 600 /dev/null /etc/strategy-trading-lab/api.env
   sudo nano /etc/strategy-trading-lab/api.env
   ```

   One `KEY=value` per line, no quotes: `DATABASE_URL`, `DATABASE_URL_UNPOOLED`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_ENDPOINT_URL_S3`, `AWS_REGION` (the same variables `neon env pull` writes to `.env.local`, but for the `production` branch).

2. The `/api/` location in nginx: copy [deploy/nginx/strategy-trading-lab.conf](../nginx/strategy-trading-lab.conf) to the server as its header says.

install.sh creates the rest (the `stlab-api` user, `/opt/strategy-trading-lab-api`, the systemd unit) on every deploy.

## On the server

```bash
systemctl status strategy-trading-lab-api
```

```bash
journalctl -u strategy-trading-lab-api -f
```

To switch back to an older release by hand: `sudo ln -sfn /opt/strategy-trading-lab-api/releases/<release> /opt/strategy-trading-lab-api/current && sudo systemctl restart strategy-trading-lab-api`.
