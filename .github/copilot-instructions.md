# Strategy Trading Lab — working on this repo

Strategy Trading Lab (STL) is a strategy lab and monitoring side app. A user picks a strategy template, reviews and tunes it, gives it a budget and runs it as a **simulation** (the app records the trades the rules call for) or **live** (the user trades at their own broker and confirms each signal). The app never places orders and never connects to a broker.

The owner and engineer is Mr Ernest.

## Dev Environments

Mac with vscode, windows with vscode and Claude Cloud session.
Tools can be installed on Mac and Windows. Ask first.

Keep the project ready for all these environments and to continue work from one to another.
If a manual step like syncing a secret or api key is needed remind that to Ernest and facilitate the steps.

Dev DB on Neon branch.

## Production environment

An oracle cloud server (see ../deploy). And Neon db.
You can manage them as an admin. Only ask for confirmation on critical or security related actions.
Ask for a credential if needed.

## Sources of truth

Read these before building a screen. Don't invent copy or behaviour that they already define.

- [docs/strategy-trading-lab-app-spec.md](../docs/strategy-trading-lab-app-spec.md): every screen and behaviour. Test in `[brackets]` are placeholders for data or copy still to be written. The spec may contain small gaps or slightly diverge from implementation decisions. You can make assumptions and explain what was done.
- [wireframes/](../wireframes/): one `.dc.html` per screen (design-tool format, not runnable). Use their markup for layout, spacing and colours.
- [design-system/cyberpunk/](../design-system/cyberpunk/): the visual style. Tokens are in `tokens.json`.

## Repo layout

- `frontend/`: React + TypeScript PWA (Vite). It runs against an in-browser mock backend for now. See `.github/instructions/frontend.instructions.md`.
- `backend/` (not created yet): C# .NET API with PostgreSQL. See `.github/instructions/backend.instructions.md`.
- `docs/`, `wireframes/`, `design-system/`: reference material. Don't edit them unless asked.

## Product rules

- **Describe, don't instruct.** No "buy now", no urgency, no price targets or return forecasts, no "recommended for you".
- **One disclaimer** wherever results or signals appear: "Not financial advice. Signals follow your rules."
- Results are always shown next to the index fund and the worst year. Past results never come without "Past results don't promise future ones."
- Words: say *stock* or *fund* and *step*, not *tranche*.
- Every strategy shows its mode (*Simulation* or *Live*). Results of the two modes are never mixed.
- Mobile first (wireframes are 390px wide), but every screen must also work on desktop, with no horizontal scrolling.
- Touch targets are at least 44 × 44px. Text meets WCAG AA contrast.

## Git workflow

- Work on a feature branch (`feature/<short-name>`, `fix/<short-name>`) and open a small PR into `main`. Don't push to `main` directly.
- One logical change per commit, with an imperative subject line ("Add Choose the mode screen").
- Merge PRs with **squash merge** (`gh pr merge --squash --delete-branch`), so `main` stays linear with one commit per PR. Write the PR title as the final commit subject (imperative, with `(#number)`), because the squash uses it. Only merge when asked.

## Feature, fixes and agentic workflow
Follow these steps for every change, without being asked. Questions and pure research don't need them. The PR is the record: the plan, every review finding and what happened to it go on the PR, not only in the chat. One session (chat) is generally one branch, one PR.

Models: you choose the model and effort for each subagent, based on how complex and how risky the change is, and you name the model in that step's PR comment. Prioritize cheaper models and lower effort unless the task is high-risk or foundational. Most cases are between Sonnet Medium or Opus Medium for complex or risky changes; for easy cases consider cheaper (Haiku).

Defaults:
- Analyze and Orchestrate: `opus`
- Implement: `sonnet`, complex case `opus`
- `/simplify`: `sonnet`.
- `/code-review`: `sonnet` for small, low-risk changes (UI text, config, isolated helpers). Use `opus` for complex logic or large diffs. Size the review to the risk, so AI effort goes where a bug costs money

### Steps
1. **Analyze.** and Orchestrate. Read the relevant code and docs, then state the plan in a few lines: what changes, which files, and any risks. Ask only when a decision is really mine to make.
2. **Implement.** Work on the session's branch. A merge that changes the application or its runtime is a release. Add or update tests, and keep `PLAN.md` and this file up to date.
3. **PR.** Commit, push, and open a pull request into `master`. Don't wait to be asked.
4. **Simplify.** Run `/simplify` in a subagent. Keep the cleanups that make the code clearer, run the checks again, and push. Post a PR comment listing what changed and what was left out, and why.
5. **Code review.** Run `/code-review <PR> --comment` in a subagent, so the findings land as inline PR comments. Fix the real ones, run the checks again, and push. Reply on each finding's thread with the fixing commit, or with why it stays as is, then resolve it. It runs after simplify, so it reviews the final code.
6. **More review rounds (only when they pay off).** Run `/code-review <PR> --comment` again on the new head when the PR is large (roughly 400+ changed lines or 10+ files), high risk, or the last round found a real bug (not nits). Stop when a round finds nothing serious, or after 3 rounds in total; then list what is still open in the closing comment.
7. **Report and stop.** Post a closing PR comment: what changed, what the reviews found and fixed, and the CI state. Then ask me before merging.

Type of feature / PR:
- **Docs only** (`docs/`, `CLAUDE.md`, README, comments): no version bump (CI skips the check), no `/simplify`, no `/code-review`. Steps 1, 3 and 7 only.
- **Normal** (UI, an isolated fix or feature, no money path): steps 1-5, with one review round. `/simplify` only when the diff has real new code (about 50+ changed lines); a one-line fix skips it.
- **High risk** or foundational work: all steps, `opus` review, a second round after any real finding.

At any time:
Be critical and give recommendations if you find incoherences, problems, ideas or potential improvements. Ideas welcome.

### Iterating

For a larger developments (multiple features) do the steps in a loop until reaching the goal. If stuck stop. 
You assess if the given goal requires multiple feature cycles and PRs.
When working in a loop you can merge PRs to integrate the changes.

### Feature acceptance

- Spec and documentation updated
- Implemented code follows good and project guidelines
- Has meaningful tests, or needs no testing
- PR done and CI passes
- Code review according to the rules
- Follows the specific BE and FE instructions

## Rules and permissions
Rules and permissions in this document should be considered guidelines. The user can always time override a rule, change a decision or grant a premission and the AI agent should accept that. Ask for clarification when needed.
