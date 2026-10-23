# Roadmap

The backlog for Strategy Trading Lab. Agents take work from **Next**, in order, one item per PR, and move it to **Done** with its PR number when it merges. Ernest sets the order; agents may add items, ideas and open decisions, and say so in their PR.

Screens and behaviour are in the [spec](docs/strategy-trading-lab-app-spec.md). Ernest's own notes are in [TODO.md](TODO.md).

## Done

- New-strategy flow, frontend: Pick a template, Review a template (all five), How we tested, Choose the mode, Set your budget, Name, preview and confirm (#1–#3, #10, #13, #19). Advanced settings is a placeholder.
- The dev database migrates itself: the API does it on startup from a launch profile, and cloud sessions run `backend/scripts/migrate-dev-https.py` (the session start hook does too) (#24).
- Admin page: switch the API between the dev and production databases (URLs only in the server's `api.env`, tested before use), the backend's build, and a DEV tag in the top bar while the dev database is in use. `deploy/backend/deploy-env.ps1` sends `api.env` to the server (#30).
- The email sender (SMTP, Gmail as in the POC) and "Send a test email" on the admin page, to the account itself only (#27).
- Admin settings page (`/admin`), like the POC's: settings from an allow-list (the five `SMTP_*` for now), secrets encrypted in the database and never shown, behind a fixed password until accounts have an admin role (#26).
- Starting a strategy: `POST /strategies` from Confirm (name, mode, budget, ticked stocks; the rest from the template's tested defaults) and `GET /dashboard`, which the dashboard (`Home`) shows. The fixed development user is Ernest, with the password `stl` for the log-in to come (#24).
- Backend: data model (#14), .NET skeleton with the first migration on Neon (#15), the five templates with the POC's tested defaults (#16), deployed to Oracle on merge (#17), the frontend loading templates from the API (#18).
- CI on PRs, frontend deploy to Oracle, app version in the top bar, reload on a new version (#4–#9, #11).
- Agentic workflow: instructions, ROADMAP, shared Claude settings and cloud setup hook (#20). Two disclaimers: short on screens seen again and again, full where the user commits (#21).

## Next

Draft order, for Ernest to confirm.

1. **Accounts: sign-up, log-in, forgot password, a first account page.** Ernest's call, right after #24. High risk, so in slices, each with an `opus` review: (a) the design, in the spec and [backend/AUTH.md](backend/AUTH.md) (docs, for Ernest's review); (b) the backend: sessions, the account endpoints, rate limits, the seeded user removed (#28); (c) the frontend screens and the guard (#31); (d) the email sender: SMTP with Gmail and an app password, as in the POC (Ernest's call; until the variables are set the links only reach the log). Replaces the fixed user, and closes `POST /strategies`, which is open on production until (b) is deployed.
2. **Catalan.** A priority, and cheaper now, while there are few screens. Move the frontend's copy into per-language files (English and Catalan) with a language switch the user picks (English by default, never guessed from the browser, as the spec says), and add `Texts/ca.json` for the templates. The API already serves template texts per language, from the user's record, so it needs a way to receive the choice before accounts exist. Start with a glossary, `docs/glossary-ca.md`, that fixes the key words (stock → *acció*, fund → *fons*, step → *pas*, signal → *senyal*, …), so every agent and screen translates them the same way. Claude drafts the Catalan; Ernest reviews it.
3. **Check the live app after a merge.** A script (bash and PowerShell) that checks production serves the merged versions: `/api/version` for the backend, and a new `/version.json` from the frontend build (today the version is only inside the bundle). Then the agent opens the live app and looks at the changed screens. Run it after every merge, in a loop before the next item. In cloud sessions the script works if the network settings allow the production domain; the visual check needs a headless browser (Playwright) in the cloud environment, otherwise it's local only. A first step towards an agent that tests the live app as a user (TODO.md → Ideas).
4. **Strategy engine for *Dip in winners* and *The comeback*** (the goal Ernest set on 2026-10-23, built as a loop of PRs; the rules work like the POC's `stage2_pullback` and `turnaround`, see `ernestbofill/trading-strategies-bot`). All high risk except where noted, each with an `opus` review:
   1. **Price fetching** (Yahoo, as decided): daily prices and the euro-dollar rate for the starter stocks, stored in the database, refreshed by a daily job after the US close, with tests on recorded responses.
   2. **The engine core:** the indicators (SMA, recent peak, 52-week high) and the *Dip in winners* signal, ported from the POC and checked against its results on the same prices.
   3. **The comeback** signal, on the same core.
   4. **Daily evaluation:** a daily job runs each running strategy, stores the status and signals, and records the paper trades of Simulation strategies, with the step and budget maths.
   5. **Email:** the signal alert and the daily overview through the existing sender, with the settings to switch them off (the overview is on by default).
   The screens that show the results are the next item.
5. **Strategy page (`StrategyDetail`)**, Live variant first as drawn, then **Confirm a signal** (`Signal`).
6. **Advanced settings** for *Dip in winners*, replacing the placeholder.

## Later

From the spec's *Not designed yet* list, roughly in the order they become useful:

- Simulation variants of Set your budget, Confirm and the strategy page.
- Web push notifications (email is in Next).
- Stock page; stock list editor, stock search and fund picker.
- Manual trades; editing and deleting trades.
- Strategy actions: go live with these settings, rename, change the budget, notifications switch, end.
- Sell signals and "Out of the market" for *Ride the long trend*.
- AI rounds: prompt editor, history, error state, comparison page.
- Backtest page and History.
- Account page, data and privacy (AI sharing, export, delete account).
- Empty, loading and error states everywhere.

## Ideas

- AI automation for continuous improvement.
- Users can give feedback.
- Referral program.

## Open decisions (Ernest)

- **Price data provider:** terms that allow commercial use, and the cost at many free users. The POC uses eToro and Yahoo.
- **Catalan copy:** Claude drafts, Ernest reviews. Is that enough, or does a native speaker review it before release?
- **An account pre-registered with someone else's email** (#28): the owner gets it back with a password reset, but until then the other person keeps it, even after the owner clicks the confirmation link. Should that link instead ask the owner to choose the password? See [backend/AUTH.md](backend/AUTH.md).
- The rest are in the spec's [Open questions](docs/strategy-trading-lab-app-spec.md#10-open-questions): regulation, AI data, the suggested prompt, web push on iPhone.
