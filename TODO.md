
## Todos from Claude to Ernest
- [ ] QA sessions (`qa/`): in the browser the agent will drive (the desktop app's built-in browser, or Chrome with Claude in Chrome), sign in once to `https://strategytradinglab.com` with the vip QA account and save its password in the browser. Check the account's email is confirmed. Then start a session with `/qa-session <account email>` from the desktop app. Optional: add `Bash(git push -u origin qa/*)` to `.claude/settings.json`'s allow list, so the agent pushes its session branch without a prompt (Claude's own edit of the settings was refused). (#153)
- [ ] Sender avatar (the grey circle next to `noreply@strategytradinglab.com`): Brevo can't set it; mail apps find it through the domain. Now: create a Google account with the existing address `noreply@strategytradinglab.com` (accounts.google.com → Create account → *Use your existing email*; it must receive one verification code) and set `frontend/public/icon-mail.png` as its picture, so Gmail shows it. Later (public beta): BIMI at your DNS host, not in Brevo: set DMARC to `p=quarantine` (Brevo's default `p=none` isn't enough), then add the TXT record `default._bimi.strategytradinglab.com` = `v=BIMI1; l=https://strategytradinglab.com/icon-mail.svg;` (Yahoo and others show it; Gmail and Apple Mail also need a paid CMC or VMC, whose URL goes in `a=`). (#147)
- [x] Review the Catalan texts. Started (names now Líders amb descompte, Empreses en remuntada, Mercats baixistes, Segueix un índex); Claude applied the same changes across the templates and the welcome page on `cat-lang-review`. A step is now *fase*, and money amounts say *import*, not *mida*. Check "Empreses en remuntada" fits one line on a template card at 390px. (#75)
- [x] GitHub, Settings → Branches for `main`: require the status checks `backend` and `frontend`, with "Require branches to be up to date before merging" (done by Ernest, 2026-11-02). (#144)
- [x] After #134 deploys: start a live strategy on the phone with "Allow notifications on this device" ticked on Confirm (since #135), allow notifications, and check a signal or reminder arrives and opens the signal. On iPhone, from the home-screen app (not checked by eye: no real push in the dev browser). (#134)
- [x] On the Mac and the PC, after #129 merges: add the line `TESTS_MIGRATE_DEV=true` to the repo's `.env.local` (only where it points at the Neon dev branch), so a local `dotnet test` brings the dev database up to date first. Add it again if `neon env pull` rewrites the file. PC: done 2026-11-01 (the first run applied #127's migration to dev); Mac: still to do. (#129)
- [x] Before #134 deploys (or right after): make a web push key pair with `dotnet run -- vapid-keys` in `backend/src/StrategyTradingLab.Api`, put `VAPID_PUBLIC_KEY` and `VAPID_PRIVATE_KEY` in `deploy/backend/production.env` and run *Deploy backend environment to Oracle*. Keep the same pair from then on. For local testing, a second pair in `.env.local`. Without it the app only emails. (#134)
- [x] After #128 deploys: delete the two stuck Index trend strategies (the one since Oct 25 and the one since Oct 30) and create them again. The full price history is there now, so the new ones' first check signals and the simulation buys at the next open. (#128)
- [x] After #103 deploys: on production `/admin/assets`, press *Delete all* (then *Sure? Delete all*), then *Download all* with 2 years. The prices stored before were weekly, monthly or quarterly bars, and nothing repairs them on its own. Check the summary, and that a stock no longer says "Not enough price history yet". (#103)
- [x] After #99 deploys: add your first name on the account page, so invites say "Ernest invited you" and are signed. Then send yourself a test invite in English and in Catalan, and open the link logged out on the phone (Sign-up with an invite wasn't checked by eye). (#99)
- [x] When the quiet launch starts: switch *Who can sign up* to *Invite only* on `/admin/invites`. (#99)
- [x] After #82 deploys: send the test email from `/admin` and check how the new HTML emails look in Gmail on the phone and on desktop (and Apple Mail or Outlook if you use them). (#82)
- [x] ~~Before merging #28: run `dotnet test` against the Neon dev branch.~~ Not needed: CI now runs every backend test, the database ones included (#32). #28 is merged. (#28, #31)
- [x] After #28 and #31 are merged: sign up on the live app with your own email, and check the confirmation email arrives. (#31)
- [x] Confirm or reorder ROADMAP → Next. (#20)
- [x] Decide: a fixed dev user to save strategies before sign-up exists, or build sign-up first? Decided: one fixed user, Ernest (#24). (#20)
- [x] Decide: the price data provider (commercial terms, cost at many free users). (#20)
      Yahoo it is.
- [x] Mac: install .NET 10. Claude installed it in `~/.dotnet` (no sudo). Optional: add `export PATH=$HOME/.dotnet:$PATH` and `export DOTNET_ROOT=$HOME/.dotnet` to `~/.zshrc` for your own terminal. (#20, #38)
- [x] After #38 deploys: on `/admin`, press *Test Yahoo* in the Prices box, then *Refresh now* and check *Last run* shows no failures; if Yahoo blocks the Oracle server's address, tell Claude. (#38)

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
- [x] Set `OWNER_EMAIL` to your account's email in `deploy/backend/production.env` and run *Deploy backend environment to Oracle*; `ADMIN_PASSWORD` is no longer used and can go. Your account (verified email) becomes the owner the next time it is used, and only the owner opens `/admin`. Locally, put `OWNER_EMAIL` in `.env.local`. After the first deploy, the database box's switch logs you out (sessions live in the database in use): log in again there. (#39)

- [x] Bounce list (item 19, #120): (1) put `BREVO_WEBHOOK_KEY=<at least 16 random letters and digits>` in `deploy/backend/production.env` and run *Deploy backend environment to Oracle*; (2) in Brevo, Transactional → Settings → Webhook → Add a webhook, URL `https://strategytradinglab.com/api/email/events?key=<the same key>`, with only *Hard Bounced*, *Invalid* and *Complaint* switched on (Transactional email, authentication None). Never paste the key in chat or a PR. Until both are done nothing is recorded and sending is unchanged. (#120)
- [x] Still to decide, in ROADMAP → Open decisions: the rest of the launch scope, ended strategies, and who the legal notice names. Pre-registered accounts settled on option 3 in #106. (#58, #106)
- [x] Before merging the own-domain PR (item 20), on the Oracle server: `sudo certbot certonly --nginx -d strategytradinglab.com -d www.strategytradinglab.com`, then copy `deploy/nginx/strategy-trading-lab.conf` from the branch to `/etc/nginx/sites-enabled/strategy-trading-lab`, run `sudo nginx -t && sudo systemctl reload nginx`, and check `https://strategytradinglab.com/api/health`. The merge deploys against the new address, so the certificate must exist first. This also applies the #106 nginx changes. (item 20)
- [x] ~~When item 4 comes up: pick a plus address of your own inbox for Claude's owner account (on dev and production), confirm it once from the email, and add its password to `.env.local` on each machine and to the cloud environment's secrets; Claude will name the variable.~~ Not needed: item 4 moved to *Ideas → A QA agent*, and Claude uses disposable dev accounts for checks (#86). (#58)
- [x] When item 19 comes up: open a Brevo account; Claude will say which DNS records and settings to add. Done: domain authenticated, sender and SMTP key set on the admin page, test email received. (#58, #115)
- [x] Review the launch plan in ROADMAP.md. Answered on 2026-10-25; recorded in `docs/decisions.md`. (#58)
- [x] Choose and buy the app's domain (needed for items 19 and 20). (#58)

- [x] *Your AI's top picks* (#70): try the suggested prompt once in the AI you use (copy it on the AI round screen), paste the answer back, and tell Claude if the reader complains or the scores look off. Then confirm or change Claude's assumptions in ROADMAP → Open decisions (anonymous sharing, the round rules, prompt v1). No secret or setting is needed. (#70)

- [x] After #46 deploys: on `/admin`, check *Notifications → EMAILS_ENABLED* is true and the mail password is set (*Send a test email*). The first overview arrives the next morning at 07:00 your time if you have a running strategy and a verified email. (#46)
- [x] ~~If branch protection on `main` requires CI checks: require `frontend` and `backend` only where they run.~~ Not needed: `main` has no branch protection. (#71)

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
- [ ] Referral program
- [ ] Agent to test the live app and review it as a user
- [ ] Playwright for e2e testing
- [x] User can give feedback
- [x] Claude test user: created a dev-only browser account; credentials are in the ignored root `.env.local`. (#50)
