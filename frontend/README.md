# Strategy Trading Lab — frontend

React + TypeScript PWA (Vite). Reads its data from the .NET API in `backend/`; `npm run dev` proxies `/api` to it on http://localhost:5184.

```bash
npm install
npm run dev
```

Start the API first (`dotnet run` in `backend/src/StrategyTradingLab.Api`), or run the app against the in-browser mock backend instead:

```bash
npm run dev:mock
```

- `src/api` — DTO types and the `api` client (`httpApi.ts`); `api/mock` stands in for the .NET backend with `npm run dev:mock` (`VITE_API=mock`, set in `.env.mock`).
- `src/features/new-strategy` — the new-strategy flow (1 / 5 … 5 / 5).
- `src/styles/tokens.css` — Cyberpunk design-system tokens (from `design-system/cyberpunk/tokens.json`).
