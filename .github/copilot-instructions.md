# Strategy Trading Lab — working on this repo

Strategy Trading Lab is a strategy lab and monitoring side app. A user picks a strategy template, reviews and tunes it, gives it a budget and runs it as a **simulation** (paper trades) or **live** (the user trades at their own broker and confirms each signal). The app never places orders and never connects to a broker.

## Implementation plan
The current implementation plan is in [IMPLEMENTATION-PLAN.md](../IMPLEMENTATION-PLAN.md).

## Sources of truth

Read these before building a screen. Don't invent copy or behaviour that they already define.

- [docs/strategy-trading-lab-app-spec.md](../docs/strategy-trading-lab-app-spec.md): every screen, its exact copy and behaviour. Quoted text is final copy; `[brackets]` are placeholders for data or copy still to be written.
- [wireframes/](../wireframes/): one `.dc.html` per screen (design-tool format, not runnable). Use their markup for layout, spacing and colours.
- [design-system/cyberpunk/](../design-system/cyberpunk/): the visual style. Tokens are in `tokens.json`.
- [IMPLEMENTATION-PLAN.md](../IMPLEMENTATION-PLAN.md): the build order. Work one step at a time and stop where a step says to stop.

## Repo layout

- `frontend/`: React + TypeScript PWA (Vite). It runs against an in-browser mock backend for now. See `.github/instructions/frontend.instructions.md`.
- `backend/` (not created yet): C# .NET API with PostgreSQL. See `.github/instructions/backend.instructions.md`.
- `docs/`, `wireframes/`, `design-system/`: reference material. Don't edit them unless asked.

## Product rules (apply to all code and copy)

- **Describe, don't instruct.** No "buy now", no urgency, no price targets or return forecasts, no "recommended for you".
- **One disclaimer** wherever results or signals appear: "Not financial advice. Signals follow your rules; you decide and trade at your own broker."
- Results are always shown next to the index fund and the worst year. Past results never come without "Past results don't promise future ones."
- Words: say *stock* or *fund* and *step*, never *ticker* or *tranche*.
- Money: budgets, steps and results are in euro (`€3,000`); stock prices are in dollars (`$412.30`).
- Every strategy shows its mode (*Simulation* or *Live*). Results of the two modes are never mixed.
- Status is never shown by colour alone: always add a word or a + / − sign.
- Mobile first (wireframes are 390px wide), but every screen must also work on desktop, with no horizontal scrolling.
- Touch targets are at least 44 × 44px. Text meets WCAG AA contrast.

## Git workflow

- Work on a feature branch (`feature/<short-name>`, `fix/<short-name>`) and open a small PR into `main`. Don't push to `main` directly.
- One logical change per commit, with an imperative subject line ("Add Choose the mode screen").
- Before opening a PR: the typecheck, lint, tests and build all pass, and you have checked the changed screens in a browser at phone and desktop width.
- Bump the version in `frontend/package.json` (and `package-lock.json`) in every PR that changes the app. It is shown in the app's top bar and nothing bumps it automatically. Bump the minor number for a new screen or feature (`0.1.0-alpha` → `0.2.0-alpha`) and the patch number for a fix or small tweak (`0.2.0-alpha` → `0.2.1-alpha`). Keep the `-alpha` suffix.
