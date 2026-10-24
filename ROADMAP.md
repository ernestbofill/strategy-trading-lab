# Roadmap

The backlog for Strategy Trading Lab. Agents take work from **Next**, in order, one item per PR, and move it to **Done** with its PR number when it merges. Ernest sets the order; agents may add items, ideas and open decisions, and say so in their PR.

Screens and behaviour are in the [spec](docs/strategy-trading-lab-app-spec.md). Ernest's own notes are in [TODO.md](TODO.md).

## Done

- Accounts: sign-up, log-in, forgot and reset password, email confirmation, and the account page, with server-side sessions and rate limits. The signed-in home (`/`) is the dashboard; sign-up starts the new-strategy flow at the template picker (`/new`). `POST /strategies` and `/dashboard` now need a logged-in user. Designed in [backend/AUTH.md](backend/AUTH.md) (#25), backend (#28), screens and guard (#31). The fixed development user is gone.
- Phone installation: Account install prompt, manual iPhone and Android instructions, and home-screen icons (#50).
- Currency: euro, US dollar or British pound, picked at sign-up and changed on the account page; new strategies start in it, and the budget screens and dashboard show it. Backtest figures and template texts stay in euro for now (Ernest's request).
- New-strategy flow, frontend: Pick a template, Review a template (all five), How we tested, Choose the mode, Set your budget, Name, preview and confirm (#1–#3, #10, #13, #19). Advanced settings is a placeholder.
- The dev database migrates itself: the API does it on startup from a launch profile, and cloud sessions run `backend/scripts/migrate-dev-https.py` (the session start hook does too) (#24).
- Admin page: switch the API between the dev and production databases (URLs only in the server's `api.env`, tested before use), the backend's build, and a DEV tag in the top bar while the dev database is in use. `deploy/backend/deploy-env.ps1` sends `api.env` to the server (#30).
- The email sender (SMTP, Gmail as in the POC) and "Send a test email" on the admin page, to the account itself only (#27).
- Admin settings page (`/admin`), like the POC's: settings from an allow-list (the five `SMTP_*` for now), secrets encrypted in the database and never shown, first behind a fixed password (#26), now behind the owner role: accounts have a role (user, vip or owner), the owner set by `OWNER_EMAIL` (#39). Vip has no privileges or invitation yet.
- Starting a strategy: `POST /strategies` from Confirm (name, mode, budget, ticked stocks; the rest from the template's tested defaults) and `GET /dashboard`, which the dashboard shows (#24).
- Backend: data model (#14), .NET skeleton with the first migration on Neon (#15), the five templates with the POC's tested defaults (#16), deployed to Oracle on merge (#17), the frontend loading templates from the API (#18).
- CI on PRs, frontend deploy to Oracle, app version in the top bar, reload on a new version (#4–#9, #11).
- Agentic workflow: instructions, ROADMAP, shared Claude settings and cloud setup hook (#20). Two disclaimers: short on screens seen again and again, full where the user commits (#21).

## Next

Draft order, for Ernest to confirm.

1. **Catalan.** A priority, and cheaper now, while there are few screens. Move the frontend's copy into per-language files (English and Catalan) with a language switch the user picks (English by default, never guessed from the browser, as the spec says), and add `Texts/ca.json` for the templates. The API already serves template texts per language, from the user's record, and the user record now has a locale, so the choice only needs a switch on the account page. Start with a glossary, `docs/glossary-ca.md`, that fixes the key words (stock → *acció*, fund → *fons*, step → *pas*, signal → *senyal*, …), so every agent and screen translates them the same way. Claude drafts the Catalan; Ernest reviews it.
2. **Check the live app after a merge.** A script (bash and PowerShell) that checks production serves the merged versions: `/api/version` for the backend, and a new `/version.json` from the frontend build (today the version is only inside the bundle). Then the agent opens the live app and looks at the changed screens. Run it after every merge, in a loop before the next item. In cloud sessions the script works if the network settings allow the production domain; the visual check needs a headless browser (Playwright) in the cloud environment, otherwise it's local only. A first step towards an agent that tests the live app as a user (TODO.md → Ideas).
3. **Strategy engine for *Dip in winners* and *The comeback*** (the goal Ernest set on 2026-10-23, built as a loop of PRs; the rules work like the POC's `stage2_pullback` and `turnaround`, see `ernestbofill/trading-strategies-bot`). All high risk except where noted, each with an `opus` review:
   1. ~~**Price fetching** (Yahoo, as decided)~~ Built (#38): daily prices and euro exchange rates in `prices_daily` and `fx_rates`, refreshed by a daily job after the US close. The schedule, *Refresh now* and *Test Yahoo* are on the admin page. Open: whether Yahoo answers from the Oracle server.
   2. ~~**The engine core:** the indicators and the *Dip in winners* signal, ported from the POC and checked against its results on the same prices.~~ Built (#43): `Engine/` with a golden test against the POC on real candles.
   3. ~~**The comeback** signal, on the same core.~~ Built (#44): `ComebackRules`, with a golden test against the POC on six real slices.
   4. ~~**Daily evaluation:** a daily job runs each running strategy, stores the status and signals, and records the paper trades of Simulation strategies, with the step and budget maths.~~ Built (#45): `Evaluation/`, run after each price refresh; signals, signal trades and `daily_evaluations`. The results (budget used, returns) from the trades came with the strategy page (item 4).
   5. ~~**Email:** the signal alert and the daily overview through the existing sender, with the settings to switch them off (the overview is on by default).~~ Built (#46): alerts and overview, `EMAILS_ENABLED` on the admin page and the overview switch on the account page. Not yet: web push, the snooze reminder, a per-strategy alerts switch (with the strategy actions), results in the scoreboard, Catalan texts.
   The screens that show the results are the next item.
4. ~~**Strategy page (`StrategyDetail`)**~~ Built, both modes: `GET /strategies/{id}` and the page, with the results from the trades (also on the dashboard cards, and live signals in *Waiting for you*). Not yet: Rename, Edit list, Tune the settings, Backtest, the stock page, the budget, notifications and end actions. Next: **Confirm a signal** (`Signal`).
5. ~~**The step includes its fee**~~ Built (Ernest's decision on #49): a signal trade splits the step into what buys shares and the fee (`TestedFee.Split`), so a €100 step is €98.80 of shares and a €1.20 fee, and budget used is exactly the steps bought. Trades made before stay as they were (fee on top). The tested results stay: counting the fee inside the step lowers a step's return by about 0.014 points per 1× of its end value (0.03 points at +100%). Not yet: Confirm a signal prefills Amount as the step minus the tested fee (with that screen).
6. ~~**Advanced settings** for *Dip in winners*, replacing the placeholder.~~ Built: each template lists its tunable settings (`Template.Settings`, words in the texts), the screen keeps the changes in the draft, Confirm shows "[n] changed", and a new strategy stores them in its params. Not yet: the backtest re-run with the user's values (needs the backtest engine; the screen shows the tested results meanwhile), asking before leaving with unsaved changes (needs a data router for `useBlocker`), *Tune the settings* on a running strategy, and the other four templates' settings. Open (#55 review): a strategy with changed settings still shows the template's past result on its card and page ("the template did +212%"), which is the tested defaults' result; once the backtest can re-run, show its own, or say it is the defaults'.
7. **Daily overview right after the price check.** Today the overview waits for 07:00 in the user's time zone (`NotificationService.OverviewHour`), while the prices and signals are ready after the US close. By default, send it as soon as the daily check has run, so there is no delay; a later, user-chosen time can come with the overview settings. Then make the copy match everywhere: sign-up and the account page ("early in the morning"), Confirm ("after the US market close"), and the spec (*Account*, *Daily overview*). Found in the end-to-end test of 2026-10-24.

## Later

From the spec's *Not designed yet* list, roughly in the order they become useful:

- Template texts and backtest figures in the user's currency (the tested defaults, "Results in euro", fees): today they stay in euro for every user.
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
- **Top bar and navigation.** The top bar wraps on phones ("STL // DASHBOARD" on two lines at 375px), "← BACK" on Pick a template right after sign-up goes back to the sign-up form, and logging out sends you to `/log-in?next=/account`, so logging back in lands on Account instead of the dashboard. Rethink the top bar and how screens link to each other as a whole.
- **Validate *How we tested* against the POC.** The backtest figures on the template pages (*Tested*, *By period*) aren't checked yet. The POC (`ernestbofill/trading-strategies-bot`) has a lot of backtest research: use it to check or replace the figures, and say on the page that each period is an independent test (fresh money at its start), which is why the periods don't add up to *All*.
- **Set your budget, with the ticked stocks** (low priority). "Enough for 6 stocks bought in full" ignores how many stocks are ticked: with 3 ticked, only 15 of the 30 steps can ever be used. Say so, and avoid "enough for 0 stocks bought in full" when a step is larger than a stock's maximum.
- **Set your budget, out-of-range values** (low priority). A typed value outside the limits is changed without a word (0 becomes €500; a step larger than the budget becomes the budget). Tell the user what changed and why, next to the field.

## Ideas

- AI automation for continuous improvement.
- Users can give feedback.
- Referral program.

## Open decisions (Ernest)

- **Price data provider:** terms that allow commercial use, and the cost at many free users. The POC uses eToro and Yahoo.
- **Catalan copy:** Claude drafts, Ernest reviews. Is that enough, or does a native speaker review it before release?
- **An account pre-registered with someone else's email** (#28): the owner gets it back with a password reset, but until then the other person keeps it, even after the owner clicks the confirmation link. Should that link instead ask the owner to choose the password? See [backend/AUTH.md](backend/AUTH.md).
- **Daily check, still open** (#45 review): waiting signals are not closed when a stock is removed, unticked or delisted, or the strategy ends (add with the strategy actions); the evaluator loads each stock's whole history per strategy (load once per stock per run when there are many strategies). Settings or budget edits mid-run: past signals are never rewritten, new ones start from the next evaluated day.
- **Daily check, replay and first run** (#45 round 2, not blocking): a signal skipped for the budget, or blocked by the no-back-dating rule, still counts as a step in the next replay, so later step numbers can run ahead of the stored signals after a params change. The first run for a strategy and stock turns every step since its start into a signal (a backlog for strategies that existed before the deploy): should the first run only look at the latest day instead?
- **Emails, still open** (#46 round 3, not blocking): a one-off send failure (SMTP timeout) is recorded as failed and not retried; the 3-day alert lookback counts calendar days (a long weekend can drop one); when email isn't set up the loops still claim and release every row each hour; "close to the line" measures to the zone's top even when the zone has vanished; the overview checkbox shows no "Saved" message.
- **Results, still open** (strategy page): sales are left out of the results until *Ride the long trend* runs (no template sells yet); the index is SPY, in USD, for every strategy; unticked stocks are hidden from the page (DATA-MODEL says dimmed; add with Edit list); the page and dashboard are cached for the session (forget them when Confirm a signal saves); the page shows the template's worst year until the strategy has a year of its own (its own worst year isn't worked out yet). The dashboard and the page work out the results on every request from all the trades and prices since the start: store a daily result when strategies get long or many.
- **The comeback once its milestones are used** (#44, not blocking): as in the POC, a buy uses up every milestone already reached, so a stock can end with budget steps left and no further signals (the status says *Holding*, as for a full budget). The spec's "one step per milestone" says the same. Keep, or should later steps come another way, and what should the screen call it?
- The rest are in the spec's [Open questions](docs/strategy-trading-lab-app-spec.md#10-open-questions): regulation, AI data, the suggested prompt, web push on iPhone.
