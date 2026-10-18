---
applyTo: "backend/**"
---

# Backend (C# .NET + PostgreSQL)

Plan: [Backend Implementation Plan](./backend/PLAN.md)

## Versions

Every PR that changes the backend bumps `<Version>` in `backend/Directory.Build.props` by one alpha step (`0.1.0-alpha.1` → `0.1.0-alpha.2`). CI fails without it. Merging to `main` deploys it: [deploy/backend/README.md](../../deploy/backend/README.md).

## Worktrees

In a worktree, apply the workspace theme in .vscode/settings.json in the worktree folder.
{
    "workbench.colorTheme": "Activate SCARLET protocol (beta)"
}
