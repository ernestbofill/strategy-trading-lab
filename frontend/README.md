# Strategy Trading Lab — frontend

React + TypeScript PWA (Vite). Reads its data from the .NET API in `backend/`; `npm run dev` proxies `/api` to it on http://localhost:5184.

```bash
npm install
npm run dev
```

Start the API first (`dotnet run` in `backend/src/StrategyTradingLab.Api`).

- For authenticated local browser checks, use the dev-only account in [the browser test account guide](../docs/dev-browser-test-account.md). Its password is stored in the ignored root `.env.local`.
- `src/api` — DTO types and the `api` client (`httpApi.ts`).
- `src/features/new-strategy` — the new-strategy flow (1 / 5 … 5 / 5).
- `src/styles/tokens.css` — Cyberpunk design-system tokens (from `design-system/cyberpunk/tokens.json`).
