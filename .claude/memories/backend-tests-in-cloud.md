# Running the backend's database tests in a cloud session

A cloud container may lack .NET but has PostgreSQL 16 (`/usr/lib/postgresql/16`) and `psql`, so the `[NeonFact]` tests can run against a local throwaway database, the way CI does:

```bash
# .NET 10, in the home folder (no sudo):
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH" DOTNET_ROOT="$HOME/.dotnet"
# PostgreSQL, with its data outside the scratchpad (the scratchpad's permissions get reset and kill the server):
D=/var/lib/postgresql/stl; mkdir -p $D && chown postgres $D
su postgres -c "/usr/lib/postgresql/16/bin/initdb -D $D/data -U postgres -A trust"
su postgres -c "/usr/lib/postgresql/16/bin/pg_ctl -D $D/data -l $D/log -o '-k /tmp -c listen_addresses=localhost' start"
psql -h localhost -U postgres -c "create user stl password 'stl' superuser"
psql -h localhost -U postgres -c "create database stl_test owner stl"
psql "postgresql://stl:stl@localhost:5432/stl_test" -q -f backend/migrations.sql
# Then, with CI's variables (see .github/workflows/ci.yml: ConnectionStrings__Default, DATABASE_URL, AWS_*), from backend/:
dotnet test
```

**Why:** Without it the database tests only run in CI, after the push. Worked on 2026-10-25 (PR #62's rework): every test passed but the three that need object storage (`NeonBranchTests` ×2, `AdminDatabaseTests.Switching_…`), which need CI's RustFS.

**How to apply:** Use it before pushing backend changes from a cloud session. A test must never write fake prices or exchange rates for a real symbol or currency: on a developer's Neon `dev` branch it would corrupt real data. (2026-10-25, #62)
