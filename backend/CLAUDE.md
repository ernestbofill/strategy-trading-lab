# Backend (C# .NET + PostgreSQL)

## Versions

- Bump `<Version>` in `backend/Directory.Build.props` in every PR that changes the backend (not only its tests or Markdown). CI fails without it.
- Same scheme as the frontend: bump the minor number for a new feature or endpoint (`0.1.0-alpha` → `0.2.0-alpha`) and the patch number for a fix or small tweak (`0.2.0-alpha` → `0.2.1-alpha`). Keep the `-alpha` suffix.
- Merging to `main` deploys it: [deploy/backend/README.md](../deploy/backend/README.md).

## Workflow

How to run it, the Neon variables and migrations: [backend/README.md](README.md).

- Before opening a PR, from `backend/`: `dotnet build` and `dotnet test` pass, and the version is bumped if the backend changed.
- Tests marked `[NeonFact]` need a database and object storage: locally the Neon `dev` variables in `.env.local`, in CI a throwaway PostgreSQL and RustFS. CI always runs them, so no manual run is needed before a merge; locally they skip without the variables. A local run first brings the dev database up to date with this checkout's migrations (`MigrateDevDatabaseAttribute`), so a stale dev database doesn't fail the tests. It runs this branch's migrations too, as `dotnet run` does: if you close a PR unmerged after its migration reached dev, roll it back with `dotnet ef database update <previous migration>`. It never runs in CI, and refuses when the database is the one `DATABASE_URL_PRODUCTION` names.
- A new migration: also regenerate `migrations.sql` (the idempotent script), and say in the PR whether it changes or drops existing data.
- The dev database migrates itself, so nobody applies a migration by hand: the API does it on startup when run from its launch profile (F5, `dotnet run`), and so does a local `dotnet test`; in cloud sessions the session start hook runs `python3 backend/scripts/migrate-dev-https.py`, and so do you right after adding a migration there. Production gets its migrations from the deploy. Never point that script at production.

## Worktrees (local only)

In a worktree, apply the workspace theme in .vscode/settings.json in the worktree folder.
{
    "workbench.colorTheme": "Activate SCARLET protocol (beta)"
}
