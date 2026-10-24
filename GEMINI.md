# Google Antigravity (Gemini) instructions

The rules for this repo are in [CLAUDE.md](CLAUDE.md), shared by every AI tool. Read it in full before you start, then read [frontend/CLAUDE.md](frontend/CLAUDE.md) or [backend/CLAUDE.md](backend/CLAUDE.md) before you change that area. Don't copy rules into this file: change them in `CLAUDE.md`. Antigravity reads both this file and [AGENTS.md](AGENTS.md) (both point to the same place), so keep them in step.

## Gemini specifics

- Always use the latest, strongest Gemini model available (Gemini 4 at the moment, in the Antigravity version that offers it; update the app if it is missing), at high thinking for analysis and high-risk work. Use a faster model only for easy work. Where `CLAUDE.md` names a Claude model or command (`opus`, `sonnet`, `/simplify`, `/code-review`), use your own equivalent, and a review pass (an agent or `gh pr` review) for the review steps. Post findings on the PR and name the model in the step's comment.
- Run commands for both shells: the repo is used from macOS and Windows (PowerShell). Prefer `npm run` and `dotnet` commands over shell one-liners.
- Check screens in the browser at phone width (390px) and desktop width when a browser is available, and say in the PR when it wasn't.
- Don't use `.claude/` (settings, hooks) for your own setup. You may read `.claude/memories/` as notes.
- Work on a `feature/<short-name>` or `fix/<short-name>` branch and open a PR into `main`, as `CLAUDE.md` says.
