# GitHub Copilot instructions

The rules for this repo are in [CLAUDE.md](../CLAUDE.md), shared by every AI tool. Read it in full before you start, then read [frontend/CLAUDE.md](../frontend/CLAUDE.md) or [backend/CLAUDE.md](../backend/CLAUDE.md) before you change that area. Don't copy rules into this file: change them in `CLAUDE.md`.

## Copilot specifics

- Copilot doesn't import files, so open the links above yourself. The files in [instructions/](instructions/) apply to `frontend/**` and `backend/**` and only point to the same area files.
- Where `CLAUDE.md` names a Claude model or command (`opus`, `sonnet`, `/simplify`, `/code-review`), use your own: pick the model by risk as it says, and use Copilot code review for the review steps. Post findings on the PR and name the model in the step's comment.
- In the coding agent (cloud), use the GitHub tools for PRs and CI. There is no browser: say in the PR that the screens weren't checked by eye.
- Don't use `.claude/` (settings, hooks) for your own setup. You may read `.claude/memories/` as notes.
