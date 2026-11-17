# Reaching Neon from a cloud session

**Dev.** `DATABASE_URL` and `DATABASE_URL_UNPOOLED` in the cloud environment point at the `dev` branch. `psql "$DATABASE_URL"` hangs and times out there (port 5432 is blocked), so use Neon's HTTPS SQL endpoint on the host from `DATABASE_URL`:

```bash
host=$(echo "$DATABASE_URL" | sed -E 's#^[a-z]+://[^@]*@([^/:?]+).*#\1#')
curl -sS -X POST "https://$host/sql" \
  -H "Neon-Connection-String: $DATABASE_URL" \
  -H "Content-Type: application/json" \
  -d '{"query":"select 1"}'
```

**Production.** It isn't reachable through the environment's strings. Use the Neon MCP connector (`run_sql` and friends, over HTTPS) and name the `production` branch on every call. Read-only queries only, as `CLAUDE.md`'s *Production* says. The connector runs in write mode with destructive tools, so only that rule keeps an agent to reads.

**Why:** The cloud sandbox only forwards HTTPS. Dev was checked on 2026-10-20 (database `stl`). The production route came out of a talk with Ernest on 2026-11-17 (#262).

**How to apply:** For dev, use the endpoint for ad-hoc checks, one statement per call. The backend's `[NeonFact]` tests use Npgsql on port 5432, so they still skip in cloud sessions. For production troubleshooting and feedback, use the connector. Never print a connection string. (2026-10-20, updated 2026-11-17)
