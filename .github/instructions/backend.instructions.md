---
applyTo: "backend/**"
---

# Backend (C# .NET + PostgreSQL)

## Versions

- Bump `<Version>` in `backend/Directory.Build.props` in every PR that changes the backend (not only its tests or Markdown). CI fails without it.
- Same scheme as the frontend: bump the minor number for a new feature or endpoint (`0.1.0-alpha` → `0.2.0-alpha`) and the patch number for a fix or small tweak (`0.2.0-alpha` → `0.2.1-alpha`). Keep the `-alpha` suffix.
- Merging to `main` deploys it: [deploy/backend/README.md](../../deploy/backend/README.md).

## Worktrees

In a worktree, apply the workspace theme in .vscode/settings.json in the worktree folder.
{
    "workbench.colorTheme": "Activate SCARLET protocol (beta)"
}
