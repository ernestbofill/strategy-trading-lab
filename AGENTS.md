# OpenAI Codex instructions

The rules for this repo are in [CLAUDE.md](CLAUDE.md), shared by every AI tool. Read it in full before you start, then read [frontend/CLAUDE.md](frontend/CLAUDE.md) or [backend/CLAUDE.md](backend/CLAUDE.md) before you change that area. Don't copy rules into this file: change them in `CLAUDE.md`.

This file also serves any tool that reads `AGENTS.md` (Google Antigravity, GitHub Copilot's coding agent).

## Codex specifics

- Codex doesn't import files, so the links above are plain pointers: open them yourself.
- Where `CLAUDE.md` names a Claude model or command (`opus`, `sonnet`, `/simplify`, `/code-review`), use your own: the strongest model for analysis and high-risk work, a cheaper one for easy work, and `/review` (or a review subagent) for the review steps. Post findings on the PR and name the model in the step's comment.
- Don't use `.claude/` (settings, hooks, launch configs) for your own setup. You may read `.claude/memories/` as notes.
- Work on a `feature/<short-name>` or `fix/<short-name>` branch and open a PR into `main`, as `CLAUDE.md` says.
