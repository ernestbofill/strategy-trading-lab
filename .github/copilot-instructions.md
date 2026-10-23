# Strategy Trading Lab — working on this repo

Strategy Trading Lab (STL) is a strategy lab and monitoring side app. A user picks a strategy template, reviews and tunes it, gives it a budget and runs it as a **simulation** (the app records the trades the rules call for) or **live** (the user trades at their own broker and confirms each signal). The app never places orders and never connects to a broker.

The owner and engineer is Mr Ernest.

It is a hobby project with no active users, which may launch publicly later. A broken screen or a failed deploy costs little, so favour progress over ceremony and keep reviews light where nothing is at stake. Still build it ready for launch: secrets, security and the product rules apply in full, and the *High risk* list stays high risk.

## Sources of truth

Read these before building a screen. Don't invent copy or behaviour that they already define.

- [ROADMAP.md](../ROADMAP.md): what is built, what comes next and the open decisions. It is the backlog: take work from it and keep it up to date.
- [docs/strategy-trading-lab-app-spec.md](../docs/strategy-trading-lab-app-spec.md): every screen and behaviour. It is work in progress, and so is the implementation:
  - Its copy is the current version, not final. You may shorten or improve it (see *UI simplicity* in the frontend instructions), except the disclaimers and "Past results don't promise future ones.", which stay word for word.
  - Text in `[brackets]` is a placeholder for data or copy still to be written.
  - It has gaps, and some parts are only ideas. Where it is unclear or doesn't fit, make a reasonable assumption, build it, and update the spec in the same PR. List each assumption in the PR description.
- [wireframes/](../wireframes/): one `.dc.html` per screen (design-tool format, not runnable). Use their markup for layout, spacing and colours.
- [design-system/cyberpunk/](../design-system/cyberpunk/): the visual style. Tokens are in `tokens.json`.

## Repo layout

- `frontend/`: React + TypeScript PWA (Vite). It talks to the .NET API. See [frontend.instructions.md](instructions/frontend.instructions.md).
- `backend/`: C# .NET API with PostgreSQL on Neon. See [backend.instructions.md](instructions/backend.instructions.md).
- `deploy/`: the Oracle server setup and deploy scripts.
- `docs/`: the spec and product docs. Keep them in step with what was built.
- `wireframes/`, `design-system/`: reference material. Don't edit them unless asked.

## Environments

Ernest works from a Mac and a Windows PC with VS Code. Claude Code runs both locally (on the Mac and the PC) and in cloud sessions. Keep the project ready for all three, so work can continue from one to another: no machine-specific paths, scripts that run on both shells, and every manual setup step written down.

| | Mac / Windows (local) | Claude Code cloud session |
| --- | --- | --- |
| Tools | Node 24, .NET 10. Ask before installing anything new. | The session start hook runs `npm ci` and `dotnet tool restore` where the tools exist. Tools it lacks are a setup step for Ernest in the cloud environment's settings. |
| Dev database | The Neon `dev` branch, variables in `.env.local` (gitignored, written by `neon deploy`). | Only if Ernest has added the variables to the cloud environment. Otherwise the API can't reach a database: say so in the PR. |
| Browser check | The built-in browser or Chrome, at phone and desktop width. | No browser: rely on tests, and say in the PR that the screens weren't checked by eye. |
| Production | Allowed (see below). | Not available: no server key. Hand it to Ernest. |
| GitHub | `gh` CLI. | The GitHub connector, or `gh` if it is logged in. |

Secrets are never committed and never pasted into chat, PRs or logs. When a step needs a secret or a manual setting (a new variable, an API key, a cloud environment setting), tell Ernest exactly what to set and where, by name, never by value. Keep working if it doesn't block you, and stop only when you can't go on without it.

Every manual step for Ernest goes in two places: the PR's *Manual steps for Ernest*, and `TODO.md` under *Todos from Claude to Ernest*, with the PR number. Ernest works from `TODO.md`, so a step only in a PR gets lost. When a step is done, tick it.

## Production

The API runs on an Oracle Cloud server ([deploy/](../deploy/)) with the Neon `production` branch. Merging to `main` deploys the backend.

You can manage them as an admin. Ask Ernest first for anything critical or security related:

- writing to, migrating or restoring the production database by hand, or deleting any Neon branch;
- changing the server's firewall, nginx, TLS, users or SSH access;
- creating, rotating or revoking secrets and credentials;
- anything that can't be undone.

Ask for a credential when you need one.

## Product rules

- **Describe, don't instruct.** No "buy now", no urgency, no price targets or return forecasts, no "recommended for you".
- **A disclaimer** wherever results or signals appear. The user sees it over and over, so it comes in two lengths:
  - Short, on screens seen again and again: "Not financial advice. Signals follow your rules."
  - Full, where the user commits (accepted once at sign-up, above the Confirm button, the legal page): "Not financial advice. Signals follow your rules; you decide and trade at your own broker."
- Results are always shown next to the index fund and the worst year. Past results never come without "Past results don't promise future ones."
- Words: say *stock* or *fund* and *step*, not *tranche*.
- Every strategy shows its mode (*Simulation* or *Live*). Results of the two modes are never mixed.
- Mobile first (wireframes are 390px wide), but every screen must also work on desktop, with no horizontal scrolling.
- Touch targets are at least 44 × 44px. Text meets WCAG AA contrast.

## Git workflow

- Work on a feature branch (`feature/<short-name>`, `fix/<short-name>`) and open a small PR into `main`. Don't push to `main` directly.
- One logical change per commit, with an imperative subject line ("Add Choose the mode screen").
- Changes Ernest has staged go into your next commit, together with yours. Don't unstage or stash them.
- Merge PRs with **squash merge** (`gh pr merge --squash --delete-branch`), so `main` stays linear with one commit per PR. Write the PR title as the final commit subject (imperative, with `(#number)`), because the squash uses it. Merge only when asked, or in a loop (see *Iterating*).

## Feature, fixes and agentic workflow

Follow these steps for every change, without being asked. Questions and pure research don't need them. The PR is the record: the plan, every review finding and what happened to it go on the PR, not only in the chat. One session (chat) is one branch and one PR, except in a loop.

Models: you choose the model and effort for each subagent, based on how complex and how risky the change is, and you name the model in that step's PR comment. Prefer cheaper models and lower effort unless the change is high risk or foundational. Most work sits between Sonnet medium and Opus medium; for easy work consider Haiku.

Defaults:

- Analyze and orchestrate: `opus`
- Implement: `sonnet`, `opus` for complex work
- `/simplify`: `sonnet`
- `/code-review`: `sonnet` for small, low-risk changes (UI text, config, isolated helpers), `opus` for complex logic or large diffs. Size the review to the risk, so AI effort goes where a bug costs money.

### Kinds of change

- **Docs and tooling only** (`docs/`, `ROADMAP.md`, `CLAUDE.md`, READMEs, instructions, `.claude/`, comments): no version bump (CI skips the check), no `/simplify`, no `/code-review`. Steps 1, 3 and 7 only.
- **Normal** (UI, an isolated fix or feature): steps 1–5, with one review round. Run `/simplify` only when the diff has real new code (about 50+ changed lines); a one-line fix skips it.
- **High risk**: strategy and budget calculations (signals, steps, results, backtests), authentication, and security. Also foundational work that much else will build on. All steps, an `opus` review, and a second round after any real finding.

### Steps

1. **Analyze and orchestrate.** Read the relevant code, docs and `ROADMAP.md`, then state the plan in a few lines: what changes, which files, the kind of change, and any risks. Ask only when a decision is really Ernest's to make.
2. **Implement.** Work on the session's branch. A merge that changes the application or its runtime is a release: bump the version as the frontend and backend instructions say. Add or update tests. Keep `ROADMAP.md`, the spec and these instructions up to date.
3. **PR.** Run the checks in the frontend and backend instructions, then commit, push, and open a pull request into `main`. Don't wait to be asked. Fill in the PR template.
4. **Simplify.** Run `/simplify` in a subagent. Keep the cleanups that make the code clearer, run the checks again, and push. Post a PR comment listing what changed, what was left out, and why.
5. **Code review.** Run `/code-review <PR> --comment` in a subagent, so the findings land as inline PR comments. Fix the real ones, run the checks again, and push. Reply on each finding's thread with the fixing commit, or with why it stays as is, then resolve it. It runs after simplify, so it reviews the final code.
6. **More review rounds (only when they pay off).** Run `/code-review <PR> --comment` again on the new head when the PR is large (roughly 400+ changed lines or 10+ files), high risk, or the last round found a real bug (not nits). Stop when a round finds nothing serious, or after 3 rounds in total; then list what is still open in the closing comment.
7. **Report and stop.** Post a closing PR comment: what changed, what the reviews found and fixed, and the CI state. Then ask Ernest before merging (in a loop, see below).

At any time, be critical: point out inconsistencies, problems and possible improvements. Ideas are welcome; add the ones worth keeping to `ROADMAP.md`.

### Iterating

When the goal needs several features, decide how to split it into PRs and run the steps above in a loop, one PR per item, taking items from `ROADMAP.md` in order.

- In a loop you may merge your own PRs once they meet *Feature acceptance*, including backend PRs, which deploy to production. After a backend merge, check that the deploy workflow passed and `/api/health` answers before starting the next item.
- High-risk PRs are merged too, once the reviews are done and CI is green. Only a question for Ernest (a decision that is his, or something the PR couldn't verify that he must check) keeps a PR open: say so in the PR and the chat. (Ernest's call, 2026-10-23: "I don't need to approve every merge".)
- Docs-only PRs are the exception: leave them open for Ernest, who reviews docs himself, unless he says to merge.
- Stop when the goal is reached, when you are stuck, or when the next item needs a decision from Ernest. Say where you stopped and why.

### Feature acceptance

- The spec, `ROADMAP.md`, `docs/decisions.md` and other docs are updated.
- The code follows these instructions and the frontend or backend ones.
- It has meaningful tests, or needs none.
- The PR is open and CI passes.
- It was reviewed as its kind of change requires.

## Tools for STL work

Tools come from Ernest's claude.ai connectors, switched in the desktop app's + → Connectors. That switch is account-wide and the default for new sessions, and the app loads connectors whatever the repo's settings say, so the repo can't limit them. Use only these for STL work:

| Need | Use |
| --- | --- |
| GitHub (PRs, reviews, CI) | `gh` locally; the GitHub connector in cloud sessions |
| Dev database, Neon branches, schema checks | The Neon connector (production: see *Production*) |
| Running and checking screens | The built-in browser, with the `.claude/launch.json` configs (`frontend`, `backend`) |
| Web research (price data, regulation, libraries) | Exa. Firecrawl, if it's on, only for legal or government sources or to scrape a page. |
| Diagrams in chat | visualize |

Don't use Gmail, Google Drive, Claude Docs or eToro (the app never connects to a broker) for STL work, even when they are on. If a task really needs one, ask Ernest first.

## Working with Ernest

- When Ernest asks for an opinion or a review ("wdyt", "review this"), write the full review in the chat first, with numbered or headed points he can quote. Change things only after he answers.

## Memory: the repo is what's shared

Claude's memory for this repo lives in the repo, in `.claude/memories/`, so it is the same on the Mac, the PC and in cloud sessions, and Ernest can see everything that was added. Claude Code's auto memory stays on, but in this repo save memories to `.claude/memories/`, never to its default folder (`~/.claude/projects/…/memory/`). If something is already in the default folder, move it here.

| What to remember | Where |
| --- | --- |
| Rules and Ernest's preferences | These instructions |
| What's built, what's next, open decisions | `ROADMAP.md` |
| Steps waiting on Ernest | `TODO.md` → *Todos from Claude to Ernest* |
| Decisions that outlive a PR, and why | `docs/decisions.md`: one dated line each, with the PR number |
| Small things learned while working that fit none of the above | `.claude/memories/` |
| Plans, review findings, what was done | The PR |

How to use `.claude/memories/`:

- One fact per file (`kebab-case-name.md`), with a short **Why** and **How to apply**, and the date and PR it came from. Add a one-line link to it in `.claude/memories/MEMORY.md`, which `CLAUDE.md` imports into every session.
- Save it in the session's branch and commit it with the PR, so Ernest reviews it like any other change.
- Update or delete a memory that turns out wrong; don't add a second one. A memory never overrides these instructions: if it turns into a rule, move it here and delete the memory.

## Rules and permissions

The rules and permissions in this document are guidelines. Ernest can override a rule, change a decision or grant a permission at any time, and the agent follows that. Ask when something is unclear.
