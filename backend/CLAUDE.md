# Backend (C# .NET + PostgreSQL)

## Versions

- Don't bump it. `deploy/backend/deploy.ps1` publishes with `0.<main's commit count>.0-alpha`; `<Version>` in `Directory.Build.props` stays `0.0.0-dev` for local builds.
- Merging to `main` deploys it: [deploy/backend/README.md](../deploy/backend/README.md).

## Workflow

How to run it, the Neon variables and migrations: [backend/README.md](README.md).

- Before opening a PR, from `backend/`: `dotnet build` and `dotnet test` pass.
- Tests marked `[NeonFact]` need a database and object storage: locally the Neon `dev` variables in `.env.local`, in CI a throwaway PostgreSQL and RustFS. CI always runs them, so no manual run is needed before a merge; locally they skip without the variables. With `TESTS_MIGRATE_DEV=true` in `.env.local`, a local run first brings the dev database up to date with this checkout's migrations (`MigrateDevDatabaseAttribute`), so a stale dev database doesn't fail the tests. It runs this branch's migrations too, as `dotnet run` does: if you close a PR unmerged after its migration reached dev, roll it back with `dotnet ef database update <previous migration>`. Without that line it migrates nothing; it never runs in CI, and refuses when the tests' database is the one `DATABASE_URL_PRODUCTION` names. Don't add `DATABASE_URL_PRODUCTION` to `.env.local` for this: a test expects it unset.
- A new migration: also regenerate `migrations.sql` (the idempotent script), and say in the PR whether it changes or drops existing data.
- The dev database migrates itself, so nobody applies a migration by hand: the API does it on startup when run from its launch profile (F5, `dotnet run`), and so does a local `dotnet test` that opted in; in cloud sessions the session start hook runs `python3 backend/scripts/migrate-dev-https.py`, and so do you right after adding a migration there. Production gets its migrations from the deploy. Never point that script at production.

## Worktrees (local only)

In a worktree, apply the workspace theme in .vscode/settings.json in the worktree folder.
{
    "workbench.colorTheme": "Activate SCARLET protocol (beta)"
}
