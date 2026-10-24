# Roadmap

The backlog for Strategy Trading Lab. Agents take work from **Next**, in order, one item per PR, and move it to **Done** with its PR number when it merges. Ernest sets the order; agents may add items, ideas and open decisions, and say so in their PR.

Screens and behaviour are in the [spec](docs/strategy-trading-lab-app-spec.md). Ernest's own notes are in [TODO.md](TODO.md). Each item in Next has its kind of change (see `CLAUDE.md`) and where the spec stands on it:

- *Spec: drawn* or *designed*: build it from the spec.
- *Spec: gap*: proposed here. Write the spec section in the same PR.

## The launch product

The goal of Next is a **minimum coherent product** that Ernest can publish and run on his own. That means:

- **A stranger can use it end to end.** They find the app, read what it does, sign up, start a strategy in either mode from any template on offer, get their emails, confirm live signals, fix a wrong trade, end a strategy, and export or delete their data. Nothing on screen leads to a dead end or to something that never runs.
- **Ernest can run it.** He sees whether the daily job ran, gets an email when it fails, sees who signed up, can close sign-ups or block an account, and gets feedback from users. Limits stop one user from loading the server.
- **It can be published.** It has legal pages, its own domain and email sender, an unsubscribe link, honest backtest figures, and a security pass behind it.

What it leaves out, so it ships sooner (an open decision for Ernest, at the bottom):

| At launch | After launch |
| --- | --- |
| *Dip in winners*, *The comeback*, *Ride the long trend*, *The big market drop* | *Your AI's top picks*: it has most of the open questions (legal review of the prompt, anonymous AI data, the answer parser). Shown as "Coming soon". |
| The starter lists, tick and untick; the default fund | "+ Add a stock", the fund picker, Edit list on a running strategy |
| Alerts by email | Web push |
| Tested results of the defaults, honestly labelled | Re-running the backtest with your settings, the Backtest page |
| Rename, end, go live, alerts switch, trades you can fix | Change the budget, Tune the settings on a running strategy, the stock page |

The path, after the regulation note's *Path by stage* ([docs/regulation-spain-eu.md](docs/regulation-spain-eu.md)): **Milestone 1** finishes the core loop, **Milestone 2** makes it publishable, then a **quiet launch** (invite only, friends), then **Milestone 3**, the **public beta**.

## Done

- Accounts: sign-up, log-in, forgot and reset password, email confirmation, and the account page, with server-side sessions and rate limits. Designed in [backend/AUTH.md](backend/AUTH.md) (#25), backend (#28), screens and guard (#31). The signed-in home (`/`) is the dashboard; sign-up starts the new-strategy flow at the template picker (#54).
- Roles (user, vip, owner); only the owner opens `/admin` (#39).
- Phone installation: Account install prompt, manual iPhone and Android instructions, and home-screen icons (#50).
- Currency: euro, US dollar or British pound, picked at sign-up and changed on the account page; new strategies start in it (#35, #51). Backtest figures and template texts stay in euro for now (Ernest's request).
- New-strategy flow: Pick a template, Review a template (all five), How we tested, Choose the mode, Set your budget, Name, preview and confirm, in both modes (#1–#3, #10, #13, #19). Starting a strategy, `POST /strategies` (#24).
- Advanced settings for *Dip in winners*: tunable settings per template (`Template.Settings`), kept in the draft, stored in the strategy's params (#55).
- Prices: daily prices and euro exchange rates from Yahoo, refreshed after the US close, with the schedule, *Refresh now* and *Test Yahoo* on the admin page (#38).
- Strategy engine: the indicators and *Dip in winners* (#43) and *The comeback* (#44), each checked against the POC on real prices.
- Daily check: runs every running strategy after each price refresh, stores each stock's status and the signals, and records the signal trades (#45). A step includes its fee (#56).
- Emails: live signal alerts and the daily overview, `EMAILS_ENABLED` on the admin page and the overview switch on the account page (#46).
- Strategy page and dashboard results, both modes: results from the trades next to the index fund and the template's worst year, live signals in *Waiting for you* (#49).
- Admin page: email settings and test email (#26, #27), switch between the dev and production databases (#30), the backend's build.
- Backend: data model (#14), .NET skeleton on Neon (#15), the five templates with the POC's defaults (#16), deploy to Oracle on merge (#17); the dev database migrates itself (#24).
- CI on PRs, frontend deploy, app version in the top bar, reload on a new version (#4–#9, #11). CI runs every backend test against a throwaway database (#32).
- Agentic workflow and instructions (#20, #41, #47). Two disclaimers: short and full (#21).

## Next

### Milestone 1 · Finish the core loop

Both modes work end to end for every template the pick screen offers.

1. **Catalan.** Normal. *Spec: designed.* Ernest's priority, and cheaper the sooner it's done. Move the frontend's copy into per-language files (English and Catalan) with a language switch the user picks (English by default, never guessed from the browser), on sign-up and the account page; add `Texts/ca.json` for the templates and Catalan email texts. The API already serves template texts per language from the user's locale. Start with a glossary, `docs/glossary-ca.md` (stock → *acció*, fund → *fons*, step → *pas*, signal → *senyal*, …), so every agent translates the same way. Claude drafts the Catalan; Ernest reviews it. Every later item then adds its copy in both languages.
2. **Check the live app after a merge.** Tooling. A script (bash and PowerShell) that checks production serves the merged versions: `/api/version` for the backend, and a new `/version.json` from the frontend build. Then the agent opens the live app and looks at the changed screens. Run it after every merge, in a loop before the next item. In cloud sessions the script needs the production domain in the network settings; the visual check needs a headless browser.
3. **Only offer templates that run.** Normal. *Spec: gap.* Today a user can start *The big market drop*, *Ride the long trend* or *Your AI's top picks*, and the daily check skips it without a word (`TemplateRules.Supports`). Show those cards on Pick a template as "Coming soon", not selectable and with no review, and refuse them in `POST /strategies`. Each engine below turns its template on. Strategies already started from them (dev only) are left as they are.
4. **Daily overview right after the price check.** Normal. Today the overview waits for 07:00 in the user's time zone (`NotificationService.OverviewHour`), while the signals are ready after the US close. Send it as soon as the daily check has run (decided in #51); a user-chosen time can come with the overview settings. Make the copy match everywhere: sign-up and the account page ("early in the morning"), Confirm ("after the US market close"), and the spec (*Account*, *Daily overview*).
5. **Confirm a signal (`Signal`).** High risk. *Spec: drawn; snooze, shares and exchange rate designed.* The core of live mode. "I did it" with date, price (the next open once known; before that empty, with the signal day's close as a reference), amount (the step minus the tested fee), fee, the exchange-rate line and "Enter shares instead"; "I skipped it" with the reason chips; Snooze ("Remind me tomorrow": notified again the next trading day, once per channel); answering later with the real date; *Missed* when the window closes (built). Saving updates budget used and results, and forgets the cached strategy page and dashboard. Links from *Waiting for you*, the stock row, the alert email and the overview.
6. **Going live needs a confirmed email.** Normal. *Spec: designed (assumed).* Alerts go by email, so on Choose the mode, Live is off until the email is confirmed: "Confirm your email to go live. [Send the link again]". `POST /strategies` refuses a live strategy from an unconfirmed account. While web push isn't built, the Live box says alerts come by email (no notification prompt); the spec's push copy waits for web push.
7. **Ride the long trend.** High risk. *Spec: designed; statuses and budget variant not drawn.* The engine (the POC's `index_trend`, checked against it with a golden test like #43): buy above SMA 200 + 3%, sell below − 3%, all in and all out, cooldown 5 days. Sales come into the results and budget used (average cost; today sales are left out, see *Results, still open*). The *Out of the market* status, the sell signal on the dashboard, in emails and on Confirm a signal (entered as shares), and Set your budget with the budget only. The fund is the template's default (SPY); the picker comes later.
8. **Your trades: see, fix and log them.** High risk. *Spec: History and manual trades are ideas; this is the first cut.* A "Signals and trades" section on the strategy page: every signal (did, skipped, missed, snoozed) and trade in date order, newest first. In live, each trade can be edited (date, price, amount or shares, fee, rate) or deleted, and "Log a trade by hand" adds a buy or a sale on one of the strategy's stocks (marked *Manual*). The rules' own position never changes; only the user's results do. This is also the signal history the regulation note asks for (design rule 7).
9. **Strategy actions: rename, alerts, end.** Normal (ending touches the daily check: an `opus` review). *Spec: gap for ended strategies.* "RENAME" in the top bar (same name rules as Confirm). An alerts switch for this strategy (emails for its signals; the overview still lists them). "End the strategy", after a confirm step: the daily check stops, waiting signals close as *Missed*, the name is free again. Ended strategies move to an "Ended" section at the bottom of the dashboard, collapsed, with results frozen at the end date; their page is read-only and has "Delete" (removes it and its trades, after a confirm step). Close waiting signals when a stock is delisted too (open since #45).
10. **Go live with these settings.** Normal. *Spec: designed, not drawn.* On a simulation's page: copies the stocks, settings and budget into the draft and opens Choose the mode with Live picked; the name gets " live" or a number. The simulation keeps running; the two keep separate histories.
11. **The big market drop.** High risk. *Spec: designed; statuses and budget variant not drawn.* The engine (the POC's bear-market accumulation, with a golden test): Normal → Drawdown (−20% from the 6-month high) → first step at +5% off the low → a step every 2 weeks, 5 by default → Done, and the reset. Statuses (gap, proposed): WATCHING "[n]% below its 6-month high" (it signals after a 20% fall and a 5% bounce), WATCHING "In a drop: [n]% above its low", BUILDING "[n] of [max] steps", DONE "This drop's steps are bought". Set your budget: "Max steps per stock" reads "Steps for the fund". It may not signal for years: the review and the dashboard card say so plainly.
12. **Honest backtest figures.** High risk (results shown to users). Check the *Tested* and *By period* figures of the four templates on offer against the POC's backtests (`ernestbofill/trading-strategies-bot`), and replace any that don't hold. Say on How we tested that each period is an independent test (fresh money at its start), which is why the periods don't add up to *All*. A strategy with changed settings says its card's past result is the tested defaults' ("With the tested settings, the template did …"), until the backtest can re-run (open since #55).

### Milestone 2 · Ready to publish

13. **Public front door.** Normal. *Spec: gap (the sitemap has it).* A logged-out visitor at `/` sees a landing page: what the app is, the two modes, what it never does (trade for you, connect to your broker, give advice), the templates, sign-up and log-in. The template reviews and How we tested open read-only without an account: they are the public methodology the regulation note asks for (design rule 7). A footer on every screen links to the legal pages. Sign-up and log-in stay where they are.
14. **Legal pages.** Docs, then Normal. *Spec: gap.* Legal notice (who runs the app and how to reach them, as LSSI-CE asks), Terms of use, Privacy policy (what is kept and why, processors: Oracle, Neon, the email provider; where data lives; how to export or delete it), the full Disclaimer, and About with a conflicts-of-interest line (Ernest's own holdings in stocks the templates watch). Only the session cookie is used, which needs no banner; the privacy policy says so, and any analytics added later must keep it that way or add consent. Sign-up records which version of the terms was accepted. Claude drafts; Ernest reviews; a lawyer before the public beta.
15. **Account: language, time zone, data and privacy.** High risk (deletes data, authentication). *Spec: listed under Not designed yet.* Language (with item 1) and time zone on the account page. "Download my data": one JSON file with the account, strategies, signals and trades. "Delete my account": asks for the password, deletes everything about the user, ends every session, and sends one last email. AI sharing comes with the AI template.
16. **Emails for strangers.** Normal. *Spec: gap.* Send from the app's own domain through a transactional email provider instead of Gmail (decided in #25 to revisit before launch), with SPF, DKIM and DMARC. A one-click unsubscribe in the overview and the alerts (link and `List-Unsubscribe` header, as Gmail and Yahoo now require of bulk senders), which turns off that email kind. Stop sending to an address that bounces, and retry a failed send once (open since #46). Needs Ernest's choice of domain and provider.
17. **Own domain.** Normal, with manual steps. The app runs on `140-238-209-178.sslip.io`. Move it to the new domain: DNS, TLS, nginx, `PublicUrl`, the PWA, the scripts and docs; keep the old address redirecting for a while.
18. **Running it: health and alerts for Ernest.** Normal. *Spec: gap.* A Health box on `/admin`: the last price refresh and daily check (time, strategies checked, failures), emails sent and failed today, and the last errors. An email to Ernest when the daily check fails or prices are more than one market day old. An outside uptime check on `/api/health` (a manual step: a free monitor). Log rotation on the server. Write down, and try once on a Neon branch, how to restore the database from Neon's history.
19. **Users and sign-ups for Ernest.** Normal (security: an `opus` review). *Spec: gap.* A Users box on `/admin`: counts (sign-ups, confirmed, running strategies by mode and template) and a list (email, role, joined, last visit, strategies). Block an account (ends its sessions, stops its emails and daily checks) and unblock it; delete it as the user would. A sign-ups setting: open, invite only, or closed. Invite only means a link Ernest creates (one per person, gives the vip role), so the quiet launch is friends only.
20. **Limits and housekeeping.** Normal. *Spec: gap.* At most 10 running strategies per user and 60 stocks per strategy (to confirm), so one account can't load the daily check or the email sender; the screens say so when a limit is reached. Delete expired sessions and links, and accounts never confirmed after 30 days (with a warning email at 23).
21. **Feedback.** Normal. *Spec: gap.* "Send feedback" on the account page and in the footer: a text box, stored and emailed to Ernest with the sender's address, so he can answer by email. A list of it on `/admin`.
22. **Security pass.** High risk. Security headers (CSP, HSTS, no framing), dependency audits in CI (`npm audit`, `dotnet list package --vulnerable`), rate limits on every public endpoint, the open decision on pre-registered accounts settled, and a `/security-review` of the whole app with its findings fixed.
23. **Every screen, finished.** Normal. Empty, loading and error states everywhere (a 404 page, an error page, the PWA offline message). The top bar and navigation as a whole: it wraps on phones ("STL // DASHBOARD" on two lines at 375px), "← BACK" on Pick a template right after sign-up goes back to the sign-up form, and logging out sends you to `/log-in?next=/account`. An accessibility pass (contrast, 44px targets, labels) at phone and desktop width.
24. **End-to-end tests of the main journeys.** Tooling. Playwright in CI, against the API and a throwaway database: sign up → start a simulation → dashboard; a live signal → confirm it → results; end a strategy; delete the account. So the loop can merge without Ernest testing by hand.

**Quiet launch.** When Milestones 1 and 2 are done: sign-ups on *invite only*, Ernest invites friends, and the feedback decides what comes next.

### Milestone 3 · Public beta

25. **Lawyer review.** Ernest. The live flow, the copy, the taglines ("Find the next Nvidia", "Take the wins, dodge the losses"), the default stock lists and the legal pages, with the questions in the regulation note (section 8). Change what the review asks for.
26. **Price data for a public app.** Ernest's decision, then Normal. Yahoo's terms don't allow commercial use (regulation note, section 5). Before opening to the public, confirm it can stay, or move to a provider whose terms allow it.
27. **Open sign-ups.** Once 25 and 26 are settled and the Catalan copy is reviewed.

## After launch

Roughly in the order they become useful:

- **Your AI's top picks**: the AI round (both modes), reading the answer and its error state, the prompt editor, the round history, the AI-sharing consent and Data and privacy switch, Advanced settings for the round. Needs the AI decisions in the spec's *Open questions* first.
- **Web push** alerts, with the iPhone home-screen step; then Choose the mode gets its notification copy back.
- **Change a running strategy**: change the budget, Edit list (unticking closes the stock's waiting signals; unticked stocks dimmed, not hidden), Tune the settings.
- **Stock page**: chart with the zone shaded, status history, trades.
- **"+ Add a stock"** and the **fund picker**, with prices fetched for new stocks.
- **Backtest engine**: re-run with the user's settings (Advanced settings' "Yours" column, and each strategy's own past result), the Backtest page, and Advanced settings for the other templates.
- Template texts and backtest figures in the user's currency.
- Daily overview settings (a time of your choice).
- AI comparison page (results by AI model, never a ranking of stocks).
- Store a daily result per strategy, once strategies get long or many (today results are worked out on every request).
- **Set your budget, with the ticked stocks** (low priority). "Enough for 6 stocks bought in full" ignores how many stocks are ticked, and can say "enough for 0 stocks" when a step is larger than a stock's maximum.
- **Set your budget, out-of-range values** (low priority). A typed value outside the limits is changed without a word. Say what changed and why, next to the field.

## Ideas

- AI automation for continuous improvement; an agent that tests the live app as a user.
- Referral program between users (paid broker referrals are not allowed, see the regulation note).
- Stop the overview for users who haven't opened the app in 90 days, with one email saying so: fewer emails nobody reads, fewer spam reports.
- A "What's new" note after a release.
- A donation link to cover the server, once there are users (allowed by the regulation note).

## Open decisions (Ernest)

- **Launch scope** (proposed in *The launch product*): launch with four templates and *Your AI's top picks* as "Coming soon"; email alerts only; no stock page, stock search, fund picker, backtest re-run or budget changes until after launch. Say what to move in or out.
- **Public template pages**: recommended yes (item 13): they explain the app before sign-up and are the methodology the regulation note asks for.
- **Domain and email provider** (items 16 and 17): a domain of your choice, and a transactional provider with EU hosting and a free tier (for example Brevo, Amazon SES or Postmark).
- **Quiet launch first**: recommended. Invite-only friends, then the public beta after the lawyer review.
- **Limits** (item 20): 10 running strategies per user and 60 stocks per strategy?
- **Ended strategies** (item 9): kept read-only on the dashboard until the user deletes them?
- **Who the legal notice names** (item 14): you as a person, with a contact address? A lawyer can confirm what LSSI-CE needs for a free hobby app.
- **Price data provider:** Yahoo for now (decided). Revisit before the public beta (item 26).
- **Catalan copy:** Claude drafts, Ernest reviews. Is that enough, or does a native speaker review it before the public beta?
- **An account pre-registered with someone else's email** (#28): the owner gets it back with a password reset, but until then the other person keeps it, even after the owner clicks the confirmation link. Should that link instead ask the owner to choose the password? See [backend/AUTH.md](backend/AUTH.md). Settle it in the security pass (item 22).
- **Daily check, still open** (#45 review): waiting signals are not closed when a stock is removed or unticked (with Edit list) or delisted, or the strategy ends (item 9); the evaluator loads each stock's whole history per strategy (load once per stock per run when there are many strategies). Settings or budget edits mid-run: past signals are never rewritten, new ones start from the next evaluated day.
- **Daily check, replay and first run** (#45 round 2, not blocking): a signal skipped for the budget, or blocked by the no-back-dating rule, still counts as a step in the next replay, so later step numbers can run ahead of the stored signals after a params change. The first run for a strategy and stock turns every step since its start into a signal: should the first run only look at the latest day instead?
- **Emails, still open** (#46 round 3, not blocking): a one-off send failure is not retried (item 16); the 3-day alert lookback counts calendar days (a long weekend can drop one); when email isn't set up the loops still claim and release every row each hour; "close to the line" measures to the zone's top even when the zone has vanished; the overview checkbox shows no "Saved" message.
- **Results, still open** (strategy page): sales are left out of the results (item 7); the index is SPY, in USD, for every strategy; unticked stocks are hidden from the page; the page shows the template's worst year until the strategy has a year of its own (its own worst year isn't worked out yet).
- **The comeback once its milestones are used** (#44, not blocking): as in the POC, a buy uses up every milestone already reached, so a stock can end with budget steps left and no further signals (the status says *Holding*). Keep, or should later steps come another way, and what should the screen call it?
- The rest are in the spec's [Open questions](docs/strategy-trading-lab-app-spec.md#10-open-questions): regulation, AI data, the suggested prompt, web push on iPhone.
