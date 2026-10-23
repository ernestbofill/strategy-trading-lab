---
applyTo: "frontend/**"
---

# Frontend (React + TypeScript PWA)

## Commands (run in `frontend/`)

- `npm run dev`: dev server on http://localhost:5173. It proxies `/api` to the .NET API on http://localhost:5184 (`dotnet run` in `backend/src/StrategyTradingLab.Api`); `npm run dev:mock` uses the in-browser mock instead (`VITE_API=mock`, set in `.env.mock`).
- `npm run preview`: serves the production build, with the same `/api` proxy
- `npm run build`: typecheck (`tsc -b`) and production build
- `npm run lint`: oxlint, which should report no warnings
- `npm test`: Vitest. Component tests use `@testing-library/react` and start with `// @vitest-environment jsdom` (the rest run in Node).

## Structure

- `src/api/`: `types.ts` holds the DTOs and the `Api` interface. `index.ts` exports the `api` client: `httpApi.ts` (the .NET API at `/api`), or `mock/`, the in-browser backend, with `VITE_API=mock`.
  - Screens talk only to `api`, never to `mock/` directly.
  - DTO shapes mirror the .NET API (camelCase JSON). Change them deliberately, together with the backend, and keep the mock in step.
  - Mock values that stand in for real data (starter lists, backtest figures) are marked as MOCK in `mock/data.ts`.
- `src/components/`: shared, screen-agnostic UI. Reuse these before writing new markup:
  - `Screen`, `TopBar`, `PrimaryAction`, `ScreenMessage`: the screen shell. Pass the one primary action as `action` and the hint or disclaimer as `note`. A form's action is `PrimaryAction form="<form id>"`, a real submit button, so Enter and password managers work.
  - `Field`: `TextField`, `PasswordField` (show/hide), `SelectField`, `Checkbox` and `FormMessage`, with the error under the field.
  - `LoadableScreen`: renders the loading, error and not-found states for screens that load data.
  - `PageHeader`, `Section`, `Columns` (main + aside from 1024px), `Tag`, `Tagline`.
  - `ChoiceCard`/`ChoiceList` (single choice, radio), `CheckList` (multi choice), `ToggleTiles` (`aria-pressed`), `ItemRow`, `NavRow`, `TextAction`.
  - `Callout`/`LabelledLine`, `RuleList` and `RichText` (`**label**` and `` `term` `` markup), `BulletList`, `KeyValueList`.
- `src/features/<flow>/`: one folder per flow; the new-strategy flow is in `features/new-strategy/`.
  - `paths.ts` holds the route builders.
  - `NewStrategyDraft.tsx` holds the choices made across the flow for the chosen template, in memory only: a reload starts again from the defaults. Choosing another template starts over (`switchTemplate`); call `reset()` when a strategy is started.
  - `useTemplate.ts` holds the data hooks.
- `src/features/account/`: sign-up, log-in, password links and the account page. `AccountContext` asks `/account/me` once at start and clears the user on a 401 (`onSessionEnded` in `api/`); `RequireAccount` is the layout route of every screen that needs a user, sending others to Log-in with `?next=` (only app paths, `safeNext`). Logging in or out forgets every cached load (`invalidateAll`). The mock starts logged in as `demo@example.com` (password "a long password") and logs its emails' links to the console.
- `src/lib/`: framework-free helpers (`format.ts`, `copy.ts` for shared copy such as `DISCLAIMER`) and `useLoadable` (cached loading).

## Conventions

- **Styling:** use CSS Modules (`Component.module.css` next to the component) and only the design tokens in `src/styles/tokens.css`. Never hard-code colours.
  - Use the global classes in `src/styles/global.css` instead of repeating their rules: `.ui-strong` (uppercase labels), `.caption`, `.well`, `.btn-primary`, `.visually-hidden`.
- **Data in, decisions in data:** branch on fields such as `template.aiRound` or `template.backtest`, not on template keys like `'ai'`.
- **Copy:** take the spec's strings. Put copy used on several screens in `src/lib/copy.ts`.
- **Format:** numbers and identifiers use the mono font. Use `eur()` and `signedPct()` from `lib/format.ts`; `signedPct` writes a true minus sign (−).
- **Accessibility:**
  - Use native inputs (radio, checkbox), visually hidden, inside a `<label>`.
  - Show visible focus with the `--border-focus` outline.
  - Make sure each control has a name a screen reader can announce.
- **Responsive:** design at 390px first, then check at 768px and 1280px+. Wider layouts use `Columns` and grid; the footer action moves to the right.
- **Back:** `TopBar` goes one step back in the browser history, whichever page you came from. Its `back` is only where it goes when the screen was opened directly.
- **Not built yet:** link to `NotBuiltYet` or render the control disabled (e.g. `TextAction` without `to`). Never leave a dead link.
- Keep components small and typed. Don't add a dependency without a clear need.
- Create components for reusable UI. But don't over-engineer it, consider if its a concept that makes sense as a reusable component.
- **UI simplicity:** show the information the user needs, and no more. People don't like to read: prefer fewer, shorter words to the spec's longer copy.

## Versioning

- Bump the version in `frontend/package.json` (and `package-lock.json`) in every PR that changes the app. It is shown in the app's top bar and nothing bumps it automatically.
- Bump the minor number for a new screen or feature (`0.1.0-alpha` → `0.2.0-alpha`) and the patch number for a fix or small tweak (`0.2.0-alpha` → `0.2.1-alpha`). Keep the `-alpha` suffix.

## Testing (Vitest + Testing Library)

- Unit-test `src/lib/` helpers and the rules in components: the last AI source can't be turned off; "Use this template" is disabled with no stocks ticked.
- Test through roles and visible text, not class names.
- Put a test next to the file it covers as `*.test.ts(x)`.

## Workflow

- Before opening a PR, from `frontend/`: `npm run lint`, `npm test` and `npm run build` (which typechecks) all pass.
- Check the changed screens in a browser at phone (390px) and desktop width. In a cloud session there is no browser: say so in the PR.

## Worktrees (local only)

In a worktree, apply the workspace theme in .vscode/settings.json in the worktree folder.
{
    "workbench.colorTheme": "Cyberpunk"
}
