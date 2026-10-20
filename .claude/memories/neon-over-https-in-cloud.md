# Reading the dev database from a cloud session

In a Claude Code cloud session `psql "$DATABASE_URL"` hangs and times out (port 5432 is blocked). Use Neon's HTTPS SQL endpoint instead, on the host from `DATABASE_URL`:

```bash
host=$(echo "$DATABASE_URL" | sed -E 's#^[a-z]+://[^@]*@([^/:?]+).*#\1#')
curl -sS -X POST "https://$host/sql" \
  -H "Neon-Connection-String: $DATABASE_URL" \
  -H "Content-Type: application/json" \
  -d '{"query":"select 1"}'
```

**Why:** The cloud sandbox only forwards HTTPS. Checked on 2026-10-20: the endpoint answered on the `dev` branch (database `stl`). The Neon connector also works over HTTPS.

**How to apply:** Use it for ad-hoc checks and inspection, one statement per call. The backend's `[NeonFact]` tests use Npgsql on port 5432, so they still skip in cloud sessions. `DATABASE_URL` points at `dev`; production is not reachable from here. Never print the connection string. (2026-10-20)
