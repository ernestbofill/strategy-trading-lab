
## Todos from Claude to Ernest
- [x] ~~Before merging #28: run `dotnet test` against the Neon dev branch.~~ Not needed: CI now runs every backend test, the database ones included (#32). #28 is merged. (#28, #31)
- [x] After #28 and #31 are merged: sign up on the live app with your own email, and check the confirmation email arrives. (#31)
- [x] Confirm or reorder ROADMAP → Next. (#20)
- [x] Decide: a fixed dev user to save strategies before sign-up exists, or build sign-up first? Decided: one fixed user, Ernest (#24). (#20)
- [x] Decide: the price data provider (commercial terms, cost at many free users). (#20)
      Yahoo it is.
- [ ] Mac: install .NET 10 (`dotnet` isn't on the PATH, so backend work here can't build or test). (#20)

- [x] Decide who reviews the Catalan copy: AI and Ernest (#22)
- [x] Check the frontend deploy after #21 shows the new disclaimers. (#21)
- [x] Cloud environment (claude.ai → Claude Code → environment settings): make sure .NET 10 is available. Done: .NET 10 is in the setup script. Node 22 stays; the app works on it and CI checks Node 24. Revisit only if a Node 22 issue turns up. (#20)
- [x] Cloud dev DB (optional): `DATABASE_URL` and `DATABASE_URL_UNPOOLED` from the Neon `dev` branch are set. Port 5432 is blocked in cloud sessions, so `psql` and the `[NeonFact]` tests don't connect; Claude queries the dev database over Neon's HTTPS SQL endpoint instead (see `.claude/memories/neon-over-https-in-cloud.md`). (#20)
- [x] Connectors (#22): turn off Gmail, Google Drive, Firecrawl and Claude in Chrome in + → Connectors while working on STL, if you want fewer tools loaded (it's account-wide). Then open a cloud session once and ask Claude for its connector status, to see whether the claude.ai connectors (GitHub, Neon) reach cloud sessions.
- [x] Memory (#22): after a few sessions, check that Claude saves memories to `.claude/memories/` and not to the default folder (`~/.claude/projects/…/memory/`), on the Mac, the PC and in cloud sessions, and that they agree with the instructions. If memories slip, set `autoMemoryDirectory` per machine in `.claude/settings.local.json` (absolute path, or `~/…`) and add a session-start check for stray memories.
- [x] Dev database: `SeedErnest` is applied, and from now on the dev database migrates itself: the API does it when run from a launch profile, and cloud sessions run `backend/scripts/migrate-dev-https.py`. Nothing to run by hand. (#24)
- [x] The seeded user Ernest (`stl`): removed entirely, with the backend slice of accounts (Ernest's call). Nothing to change by hand. (#25)
- [x] Global `~/.claude/settings.json` (Mac): the auto-mode environment describes the trading bot repo (default branch `master`, trusted repo `trading-strategies-bot`), and the default model is Sonnet. Move the bot's environment into that repo's settings, and pick the default model for this repo. (#22)

- [x] Review the accounts design. Decided: all recommended answers. (#25)
- [x] Email: Gmail SMTP with an app password, as in the POC. (#25)
- [x] Email settings: set on `/admin` instead of api.env (#26); the test email works (#27).
- [x] Database switch (#30): copy `deploy/backend/production.env.example` to `deploy/backend/production.env` (gitignored) and fill it in: the production branch's variables (as in today's `api.env`), `DATABASE_URL_DEV` (the dev branch's pooled URL) and `ADMIN_PASSWORD` (a long one of your own). Then run *Terminal → Run Task → Deploy backend environment to Oracle* (or `deploy/backend/deploy-env.sh` on the Mac, `deploy-env.ps1` on Windows). Until `ADMIN_PASSWORD` is there the page can't switch the database on the server. Optional, locally: `DATABASE_URL_PRODUCTION` in `.env.local` lets your local API switch to production.
- [ ] Set `OWNER_EMAIL` to your account's email in `deploy/backend/production.env` and run *Deploy backend environment to Oracle*; `ADMIN_PASSWORD` is no longer used and can go. Your account (verified email) becomes the owner the next time it is used, and only the owner opens `/admin`. Locally, put `OWNER_EMAIL` in `.env.local`. After the first deploy, the database box's switch logs you out (sessions live in the database in use): log in again there. (#39)

## General
- [ ] Split FE and BE repos?
- [x] Instructions from STAB
- [x] CI / CD on github

## Minor
- [ ] On Oracle Cloud, review if it should be more isolated from the trading bot.
- [ ] Admin ssh
- [x] Fix git history 
- [x] worktrees template
- [x] Review the free domain and ssl certificate.
  - https://certbot.eff.org/
  - [sslip](https://nip.io/)

## Ideas
- [ ] AI automation for continuous improvement.
- [ ] User can give feedback
- [ ] Referral program
- [ ] Agent to test the live app and review it as a user
- [ ] Playwright for e2e testing
- [ ] Claude test user
