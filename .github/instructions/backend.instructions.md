---
applyTo: "backend/**"
---

# Backend (C# .NET + PostgreSQL)

## Versions

- Bump `<Version>` in `backend/Directory.Build.props` in every PR that changes the backend (not only its tests or Markdown). CI fails without it.
- Same scheme as the frontend: bump the minor number for a new feature or endpoint (`0.1.0-alpha` → `0.2.0-alpha`) and the patch number for a fix or small tweak (`0.2.0-alpha` → `0.2.1-alpha`). Keep the `-alpha` suffix.
- Merging to `main` deploys it: [deploy/backend/README.md](../../deploy/backend/README.md).

## Workflow

How to run it, the Neon variables and migrations: [backend/README.md](../../backend/README.md).

- Before opening a PR, from `backend/`: `dotnet build` and `dotnet test` pass, and the version is bumped if the backend changed.
- Tests marked `[NeonFact]` need the Neon `dev` variables in `.env.local` and skip without them (CI, and cloud sessions without the variables). Say in the PR when they were skipped.
- A new migration: also regenerate `migrations.sql` (the idempotent script), and say in the PR whether it changes or drops existing data.
- The dev database migrates itself, so nobody applies a migration by hand: the API does it on startup when run from its launch profile (F5, `dotnet run`); in cloud sessions the session start hook runs `python3 backend/scripts/migrate-dev-https.py`, and so do you right after adding a migration there. Production gets its migrations from the deploy. Never point that script at production.

## Worktrees (local only)

In a worktree, apply the workspace theme in .vscode/settings.json in the worktree folder.
{
    "workbench.colorTheme": "Activate SCARLET protocol (beta)"
}
