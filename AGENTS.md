# Codex and Antigravity (Gemini) instructions

The rules for this repo are in [CLAUDE.md](CLAUDE.md), shared by every AI tool. Read it in full before you start, then read [frontend/CLAUDE.md](frontend/CLAUDE.md) or [backend/CLAUDE.md](backend/CLAUDE.md) before you change that area. Don't copy rules into this file: change them in `CLAUDE.md`.

OpenAI Codex and Google Antigravity (IDE 1.20.5 or later) read this file. GitHub Copilot's coding agent reads it too, next to its own [.github/copilot-instructions.md](.github/copilot-instructions.md).

## For every tool that reads this file

- These tools don't import files, so the links above are plain pointers: open them yourself.
- Where `CLAUDE.md` names a Claude model or command (`opus`, `sonnet`, `/simplify`, `/code-review`), use your own equivalent: your strongest model for analysis and high-risk work, a cheaper one for easy work, and a review pass for the review steps. Post findings on the PR and name the model in the step's comment.
- Don't use `.claude/` (settings, hooks, launch configs) for your own setup. You may read `.claude/memories/` as notes.
- Work on a `feature/<short-name>` or `fix/<short-name>` branch and open a PR into `main`, as `CLAUDE.md` says.

## Codex

- Use `/review` (or a review subagent) for the review steps.

## Gemini (Antigravity)

- Always use the latest, strongest Gemini model available: Gemini 4 at the moment, in the Antigravity version that offers it (update the app if it is missing). Use high thinking for analysis and high-risk work, and a faster model only for easy work.
- Run commands that work in both shells: the repo is used from macOS and Windows (PowerShell). Prefer `npm run` and `dotnet` commands over shell one-liners.
- Check screens in the browser at phone width (390px) and desktop width when a browser is available, and say in the PR when it wasn't.
