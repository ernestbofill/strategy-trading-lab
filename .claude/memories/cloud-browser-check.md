# Browser checks in a cloud session: Playwright with mocked API replies

2026-11-17, PR #264.

Cloud sessions have headless Chromium and Playwright (`require('playwright')` works from any folder; never `playwright install`). The local API can't reach the dev database (port 5432 is blocked), so run `npx vite` in `frontend/` and answer `/api/*` in the page with `page.route(u => new URL(u).pathname.startsWith('/api/'), …)`. The screen needs at least `/api/version`, `/api/account/me` and its own endpoints (strategy page: `/api/strategies/{id}`; stock page: also `/api/strategies/{id}/stocks/{asset}/chart`). Build the JSON from the DTOs in `frontend/src/api/types/` and the template texts in `backend/.../Templates/Texts/en.json`. Unmocked calls (`/api/templates`, `/api/dashboard`) can 404 without breaking the page.

**Why:** the old rule ("no browser in a cloud session") was wrong (Ernest, #264), and a mocked check caught a label covering the price line that tests didn't.

**How to apply:** screenshot each changed screen at 390px and 1280px wide (deviceScaleFactor 1 for your own look), check `scrollWidth` for horizontal scrolling, and say in the PR that the check used mocked data. Keep the script in the scratchpad, not the repo.
