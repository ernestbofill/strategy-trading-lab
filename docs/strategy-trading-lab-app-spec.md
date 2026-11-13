# Strategy Trading Lab: app spec for implementation

Oct 15, 2026 · @Ernest

Is the productized version of https://github.com/ernestbofill/trading-strategies-bot

This is the brief for the agent building the Strategy Trading Lab app. It describes what the app is, every screen, how the screens connect, what each one does, and the exact copy. Build from this file.

- **Wireframes:** [Strategy Trading Lab wireframes · Cyberpunk](../wireframes/). Their source files are one `.dc.html` file per screen (named in the table in section 4) plus `canvas.json`, which lists the screens and the designer's notes. They are in the design tool's own format and don't run on their own, but their markup shows each screen's layout, copy and interactions.
- **Visual style:** use the [Cyberpunk design system](../design-system/cyberpunk/).
- **Product background:** `stab-product-user-journey.md`. **Regulation:** `regulation-spain-eu.md`.
- **Strategy logic:** already exists in the POC (`ernestbofill/trading-strategies-bot`, see `docs/PLAN.md`).

**How to read this spec.**
- Text in square brackets, such as `[+X.X%]` or `[Stock name]`, is a placeholder for live data or for content still to be written.
- Everything else in quotes is final copy, to be used exactly as written. Section headings in the wireframes are written in normal case and shown in capitals by the style.
- Parts marked **(not drawn)** come from the user journey and have no wireframe yet. Their copy is a draft: use it, but expect it to change once the screen is designed.

## 1. What the app is

Strategy Trading Lab is a strategy lab and a monitoring side app. A user picks a **strategy template**, sees how it did in the past, tunes its rules, gives it a budget and a name, and runs it as their own **strategy** in one of two **modes**:

- **Simulation:** the app records simulated trades on real prices, every time the rules call for one, and shows how the strategy does from today. No signal alerts, just the daily summary. Nothing happens outside the app.
- **Manual** (`live` in the code, the API and the database; called *Live* until 2026-11-09): the user follows the strategy themselves, at their own broker with real money or just to track it. The app sends each signal, the user says whether they acted on it or skipped it (or snoozes it until the next day), and the app records those trades and tracks the results.

There is no broker sync in either mode. The app never places orders and never reads the user's broker account; in live mode it knows only what the user confirms. It gives no trading advice.

## 2. Words

| Word | Means |
| --- | --- |
| **Strategy template** | An idea and its rules, as offered in the catalogue, with our tested defaults. Picked, reviewed, backtested and tuned. Templates never change because of what a user does. |
| **Strategy** | The user's own copy of a template: their name for it, their stocks, settings and budget, running in one mode from the day they start it. It keeps its own trades and results. A user can run any number of strategies, several from the same template. |
| **Mode** | *Simulation* or *Manual*. Fixed for the life of a strategy; following a simulation manually means starting a new strategy. |
| **Signal** | The moment a strategy's rules are met for a stock: a buy step, or, for *Index trend*, a sale. In simulation the app acts on it by itself; in live it asks the user. |
| **Step** | One buy of the step size, fee included: a €100 step is about €98.80 of shares and a €1.20 fee. A stock gets at most the max steps per stock. |
| **Discount zone** | For *Winners at a discount*: the range 5-15% below a stock's recent peak, where its rules signal. |
| **AI round** | For strategies from *Your AI's top picks*: every 14 days by default the user runs our prompt in their own AI service and pastes the answer back. |
| **Backtest** | How a template did in the past with given settings. It belongs to the template and settings, not to a running strategy. |

The words *ticker* and *tranche* never appear in the app: use *stock* (or *fund*) and *step*.

## 3. Product rules

These apply to every screen, email and alert.

- **Describe, don't instruct.** Template names describe the situation the rules look for. On a template's page the strategy is the subject ("It signals when…"), never the user. Never "buy now", no urgency, no price targets or return forecasts.
- **Signals report rules.** Every signal says which rule was met, the facts that met it and what that means under the user's settings.
- **The user owns the setup.** Lists, settings and budgets are the user's choices, starting from our tested defaults: "Choose stocks to watch", "Under your settings", never "recommended for you".
- **No promises.** No claims that markets or stocks will rise or recover. Results are always shown next to the index fund and the worst year. On screen the index fund is named **"S&P 500 fund"**, with a small "i" after the label that opens "Buy and hold: the same money put into an S&P 500 fund (SPY) on the same days, never sold." (#95). The explanation stays hidden until asked for, so the figures stay short.
- **Numbers:** prices and amounts to the cent (2 decimals); every other number (shares, exchange rates, percentages) to at most 3 decimals (#95).
- **One disclaimer, in the site footer (#105).** Every screen ends with the short disclaimer, "Not financial advice. Signals follow your rules.", so no screen repeats it or adds the full one or "Past results don't promise future ones.": the app is text heavy and the Disclaimer page holds the details. The full one, "Not financial advice. Signals follow your rules; you decide and trade at your own broker.", is accepted once at sign-up and leads the Disclaimer and Terms pages. Emails keep the short one at the foot. Where a screen below still says "the disclaimer" or "Past results don't promise future ones.", the footer covers it; the wireframes show it everywhere; this rule wins.
- **No broker sync.** No broker links, no prefilled orders, no reading of the user's account.
- **Plain language.** Real trading terms stay, each explained in brackets the first time it appears, e.g. "SMA 200 (average price over the last 200 days)".
- **Mostly US markets, euro results.** v1 covers the POC's stocks and funds (mostly US-listed, a few listed in Europe and Hong Kong), plus any stock or fund a user adds from the same exchanges. Budgets, steps and results are in euro (`€3,000`), converted at the day's exchange rate; stock prices are shown in the listing's currency (`$412.30`), as at the user's broker. Other currencies may follow; the data already records the currency of every amount.
- **English and Catalan.** v1 ships in both. The user picks the language (nothing is guessed from the browser) and can change it in the account. Before log-in, the Welcome and Pick a template screens show the other language in the top bar ("Català" or "English"), one tap to switch. Emails and notifications follow it, and so do their links: every link in an email or invite message starts with its language (`/ca/sign-up?invite=…`, `/en/strategies/…`), so it opens the app in the language the email was written in, before anyone logs in. The app keeps that language on the device and drops the prefix from the address; a signed-in account's own language still wins. `/en` and `/ca` alone open the app's home in that language. Quoted copy in this spec is the English; the Catalan copy is still to be written.
- **Mode always labelled.** Every strategy shows *Simulation* or *Manual*; results of different modes are never mixed.
- **Free for now.** No paywall, plans or pricing.
- **Mobile first.** The wireframes are 390 px wide. Every screen must also work on desktop, with no horizontal scrolling at any width.
- **Functional style rules.** One primary action per screen, at the bottom. Selected items look clearly different from unselected ones. Touch targets at least 44 × 44 px, including − / + steppers. Status is never shown by colour alone: every status has a text label. Text meets WCAG AA contrast.

## 4. Screens and navigation

| Screen | Wireframe file | Reached from | Goes to |
| --- | --- | --- | --- |
| Welcome (marketing highlights) (not drawn) | – | App start (`/`) for unauthenticated guests; `/welcome` | Pick a template ("Try as guest"), Sign-up ("Create an account"), Log-in |
| Sign-up (not drawn) | – | App start for new users; "Create an account" on Log-in or Welcome | Check your email, then Pick a template |
| Log-in (not drawn) | – | App start for returning users; any screen when the session has ended | Dashboard |
| Forgot and reset password (not drawn) | – | "Forgot your password?" on Log-in; the link in the email | Log-in |
| 1 · Pick a strategy template | `Main` | Sign-up; "+ New strategy" on the dashboard | The picked template's review |
| 2 · Review a template (one per template) | `Review`, `ReviewComeback`, `ReviewDrop`, `ReviewTrend`, `ReviewAI` | Pick screen "Continue" or "Learn more"; Edit links on Confirm | Advanced settings, How we tested, Choose the mode |
| 2b · Advanced settings | `Advanced` | Review; "Tune the settings" on the strategy page; Confirm | Back to where it came from |
| 2c · How we tested | `HowTested` | "How we tested ›" on a review (not on the AI review) | Back to that review |
| 3 · Choose the mode | `Mode` | Review "Use this template"; "Follow it manually" (built, #92; not drawn) | Set your budget |
| 4 · Set your budget | `Budget` | Choose the mode; Confirm | Confirm |
| 5 · Name, preview and confirm | `Confirm` | Set your budget | Dashboard |
| Dashboard (home) | `Home` | Confirm; app start for returning users | Strategy page, Confirm a signal, AI round, Pick a template |
| Strategy page | `StrategyDetail` | A strategy card on the dashboard | Stock page (not drawn), stock list editor, Advanced settings, Backtest (not drawn), Confirm a signal |
| Confirm a signal (live) | `Signal` | Waiting for you on the dashboard; a stock row; a signal alert or the summary | Strategy page |
| AI round | `AIRound` | Waiting for you on the dashboard; the AI-round notification | Dashboard (simulation); Confirm a signal (live) |

**Guest / Unauthenticated visitor:** `/` opens the Welcome page, introducing Strategy builder, Simulation, and Trading tracker with interactive expandable preview decks. Guests can explore the new-strategy flow without an account (`/new`), register (`/sign-up`), or log in (`/log-in`).

**New user:** sign-up → pick → review → (advanced settings, how we tested) → mode → budget → confirm → dashboard.

**Adding a strategy:** dashboard "+ New strategy" → the same screens. All templates are shown, with "You run [n] from this template" on templates already in use (not drawn).

**Following a simulation manually (built, #92; not drawn):** "Follow it manually" / "With these settings" on a running simulation's page fills the new-strategy draft from it and opens Choose the mode with Manual selected. The draft takes its template, its stocks (starter-list stocks it doesn't watch come unticked, stocks it added come added), its fund, the settings that differ from the tested defaults, its budget, step and max steps, an AI strategy's sources and instructions, and its currency (the copy keeps it, whatever the account's). The name is the simulation's with " (manual)" (the same in Catalan), numbered while a running strategy has it ("Zscaler & friends (manual) 2"). Every screen of the flow can still change it. An account without a confirmed email falls back to Simulation on Choose the mode, which says why. `POST /strategies` keeps `sourceStrategyId` for a live strategy only, and refuses one that isn't the user's own simulation (`errors.sourceStrategyId`). The simulation keeps running or can be archived; the two keep separate histories. A live copy's page shows "Copied from [simulation name]", a link to it, while the simulation exists.

**Scroll.** A new page opens at its top; a tab or filter kept in the address doesn't move it. Browser Back and Forward leave the position to the browser (it may land higher on a page that is still loading), and a link to a section (`#notifications`) opens at that section (F-026).

**Headers.** Pick a template (1 / 5) has "DASHBOARD" on the left ("WELCOME" for a guest, with the language and "LOG IN" on the right instead of the step). Steps 2 to 5, and the template's How we tested, Advanced and prompt screens, have "← BACK" on the left, up to the step before, and the step or the template's name on the right.

**The top bar (every screen, Ernest 2026-11-08, replaces #152's back and up).** One line with two sides; text too long for it ends in "…" and never wraps, the right side giving way first.

- **Left: home, up, or a title.**
  - *Home*, a direct link with no arrow: "DASHBOARD" signed in, "WELCOME" for a guest ("STL" for the moment the account loads). It is on every screen whose parent is home: the account, `/admin` and the admin pages, the legal pages, Pick a template, a strategy, and Welcome when signed in.
  - *Up*, "← [PARENT]": one level up inside a section, always to the same parent, never back in the history. "← STOCK" on a signal of a stock list (its stock page; a fund strategy's signal has "← STRATEGY"), "← STRATEGY" on a stock, AI round, AI rounds, and the strategy's settings and stock list; "← AI ROUND" on the strategy's prompt; "← LOG IN" on Forgot password; "← BACK" on the new-strategy steps, whose parent is the step before. The browser's own Back still goes to the previous screen.
  - *Title*: the dashboard shows "STL // WELCOME", a link to Welcome. The sign-in screens (log in, sign up, password, email) and Welcome for a guest show their name.
- **Right: one label or one or two links.** Labels are muted (the step, a date, the strategy's name); links are text actions. Left empty, it is "ACCOUNT" signed in or "LOG IN" for a guest (coming back to the page). The account shows "ADMIN" for the owner and nothing for others; the admin pages show "ADMIN", a link to `/admin`; a strategy shows "RENAME".
- The arrow is drawn, not the "←" character, so it sits on the text's middle on iPhone too.

## 5. The templates

Five templates. The first four exist in the POC; *Your AI's top picks* is new. Use the POC defaults below as the tested defaults. Where the screen copy simplifies a rule, the POC rule is what the engine runs.

| Template | POC key | Tagline | Invests in | Signals |
| --- | --- | --- | --- | --- |
| Winners at a discount | `stage2_pullback` (Leader pullback) | "Buy the dip" | A list of stocks | Buys only |
| The comeback | `turnaround` (Confirmed turnaround) | "Stocks on the rise" | A list of stocks | Buys only |
| Bear markets | Bear-market accumulation | "When the market is on sale" | One S&P 500 fund (an ETF) | Buys only |
| Index trend | `index_trend` | "Take the wins, dodge the losses" | One broad index fund | Buys **and sells** |
| Your AI's top picks | new | "Let your AI do the reading" | A list of stocks | Buys only |

### Engine defaults per template

**Winners at a discount (Leader pullback)**
- Candidate (trending): the recent peak is at least 50% above the lowest close of the last 168 trading days (~8 months); SMA 50 > SMA 150 > SMA 200; the SMA 200 is higher than 20 trading days ago. (The price itself isn't checked against the SMA 50 here; the SMA 50 is the zone's floor.)
- Signal: price in the discount zone, 5–15% below the recent peak (highest price of the last 20–60 days; the screen exposes one number, default 60, the POC's), while still above the SMA 50.
- Signal ends: the price leaves the zone (back up to the old high, or below the SMA 50).
- Later steps: one per signal window, at least 10 calendar days (cooldown) after the last one. The POC's default cooldown is 20; 10 is Ernest's setting there.

**The comeback (Confirmed turnaround)**
- Candidate (beaten down): at least 30% below its 52-week high, and below the SMA 200.
- Signal: the close is at least 15% above the SMA 50, and 5-day average volume is at least 20% above the 50-day average.
- Signal ends: the close drops back under the signal line, or the price is more than 8% above it.
- Later steps: one per milestone (back above the SMA 200, golden cross, within 15% of the 52-week high, breakout after an 8% pullback), each within 8% above its level. Cooldown 5 days.

**Bear markets (Bear-market accumulation)**
- Normal → Drawdown (−20% from the high of the last 6 months) → track the new low → Signal (+5% above the lowest low; first step) → Accumulating (one step every 2 weeks, 5 steps by default) → Done.
- Resets on a new high, or once the cycle's buys are done and the drop was first seen at least 126 trading days ago; then a new 20% fall starts a new cycle.

**Index trend (`index_trend`)**
- Buy signal: the close is more than 3% above the SMA 200. Sell signal: the close is more than 3% below it. In between, the trend stays what it was.
- Buy signal ends: the close drops back to or below the buy line (+3% above the SMA 200).
- All in, all out: one buy of the whole budget; after a sale, the next buy reinvests what the sale returned. Cooldown 5 days. One buy signal per uptrend.
- No stop-loss: the sell line is the stop.

**Your AI's top picks (new)** (built)
- Every 14 days (default; 1 to 60 days on Advanced settings), the user runs the AI round (section 6, AI round). The app runs no AI itself. The first round is due the day the strategy starts.
- The user can also run an **extra round** at any time before the scheduled one is due (at most one a day). It is dated the day it was run and leaves the schedule as it was; its live signals end when the scheduled round is due.
- After each round, the top-scored stock gets a signal for one step (default top 1; 1 to 3 on Advanced settings), within max steps per stock and the budget. A stock that already has all its steps passes its turn to the next score down (assumed). A tie goes to the stock with fewer steps, then by name. A stock with no stored price yet can't get a signal, and passes its turn too.
- A due round can be run from its due date for a week, or until the day before the next is due when rounds come more often (a daily round only on its due day). Not run by then, it is marked *Skipped*: no signal. The user can also skip a due round themselves, in either mode. The next round comes the set number of days after the due date, whether the round was run or skipped.
- The signal's date is the last finished US session (its close is the one shown).
  - **Simulation:** the signal trade is recorded at once, at the **latest available price**: "Use these scores" first fetches the picks' prices (today's bar while the market is open), and falls back to the latest stored close if the price source fails or takes over 10 seconds. The trade is dated that bar's day (or the signal's session, if the bar is older). Without an exchange rate it waits for the next open, as live does. (Changed in #76; it used to wait for the next open.)
  - **Live:** the rules' own trade (*The rules alone*) is at the **first open after the round was run** (a round run while the market is open trades at the next day's open, so the rules never buy at an open that had already passed).
- A live AI signal waits until the next round is due; still unanswered then, it is *Missed*.
- No backtest: results start on the day the strategy starts.

**Exits.** Only *Index trend* sells. The POC's stop-loss and trailing stop for Leader pullback and Confirmed turnaround are not part of the product. In buy-only templates, a user who sells at their broker logs the sale by hand (live).

### How the two modes run

- All rules are checked **once a day, after the US market close**, as in the POC.
- **A day is a US market day** (New York trading calendar): start dates, signal dates, snoozes and "the next day" all follow it. The user's time zone only sets when emails arrive.
- **A first check looks at the latest day only.** The first time the daily check runs a strategy's stock, it treats the stock as just added: it evaluates the latest finished day, and signals start from there, with no backlog of past signals (as for a stock added to a running strategy). It matters for strategies that existed before their template's engine ran. Later checks replay the rules from that first day (or from an earlier signal, for a strategy that already had a backlog), so step numbers count only the steps since. The same holds for a new strategy: its first check evaluates the latest finished session (or the next session when prices refresh), and a day the check missed isn't caught up. Days checked before the stock had a long enough price history don't count as checked: once the history is there, the next check is the stock's first real one. After that every checked day counts.
- **Checked at once.** Starting a strategy, or saving its list with a stock added or ticked again, asks for its check right away instead of at the next daily one (feedback, 2026-11-12): the prices its stocks lack are fetched (each stock not fetched since the last finished session's bar was final), then its rules run up to the last finished session, as the daily check would. A signal there trades at the next open, which is still to come. While a US session is in progress (09:30–16:30 New York) that open has passed, so before a stock's rules run the check fetches its latest price (not stored in the price history; only for a stock that can get a new signal), and a signal it finds trades at that price instead, dated that day (Ernest, #220): never at an open the user could already see. This holds for a simulation's trades and for the rules' own trades of a Manual strategy. A signal found before the open (by the evening run) still trades at the open. A stock without a latest price from after the last finished session (the fetch failed, nothing traded yet today, a holiday) is left to the daily run after the close, untouched. It runs in the price worker, one at a time with the daily run (which keeps its time), and a failure leaves it to that run. A signal it finds is pushed right after it, like the daily check's (Ernest, 2026-11-12); its email keeps the notification hour. The strategy and stock pages ask again every 5 seconds for a minute while a stock reads "Not checked yet". When the check at once left a stock to the daily run during a session, the API marks it `afterClose` until the session ends: its row and its stock page read "Checked after the US session ends" instead, and the pages don't ask, so it doesn't look stuck (feedback, 2026-11-12). Accepted inaccuracies: the simulation page still says "Results come from trades at the next day's opening price", and the S&P 500 comparison still buys at that day's open.
- **Signals follow the rules, not the user's trades.** Each strategy keeps the rules' own position (its signal trades), and signals come from it, in both modes. After a skip, the rules carry on as if the step had been bought (the next signal for that stock is the next step), and a *Index trend* sell signal arrives even if the user never bought.
- **Simulation:** the app records every trade the rules call for, at the next trading day's opening price, with the tested fee per trade, about 0.2% of the amount plus 1 in the strategy's currency (€1, $1 or £1; rounded, not converted). The fee is inside the step: amount plus fee is the step (the backtest charged it on top, which changes its results by a fraction of a point). Results come from those signal trades.
- **Live:** the app sends each signal and the user confirms it. Results come only from confirmed trades at the prices the user entered; the rules' own position is shown next to them.
- **A stock the strategy no longer follows** (delisted, unticked or removed): the daily check closes its waiting signals as *Missed*, ending on the last session (#91). It no longer checks the stock; trades in it stay in the results.
- **Budget used** is what the buys cost (with fees) minus the cost of the shares sold since, at average cost: a sale frees budget again. Simulation counts signal trades; live counts only confirmed and hand-logged trades. When the budget is used up, the strategy stops signalling buys.
- **Budget and settings can change while a strategy runs** (strategy page). The new values apply from then on. Lowering the budget below what is already used, or the max steps below a stock's current step, stops further buys; nothing is sold.

## 6. Screen by screen

### Welcome (marketing highlights) (not drawn)

- **Entry point:** `/` for unauthenticated visitors, and `/welcome`. Authenticated visitors navigating to `/` continue directly to the Dashboard.
- **Top bar:** "STL // STRATEGY TRADING LAB" on the left; the language and "LOG IN" on the right. Signed in: "DASHBOARD" · "ACCOUNT" (or "← BACK" to `/new` when opened from `/new` via `?from=new`).
- **Hero:**
  - Headline: "Trade rules, not emotions."
  - Lead: "A transparent strategy lab. Pick tested templates, run real-time simulations without risk, and track live signals with zero broker access."
- **Interactive 3-pillar feature deck:** All three core capabilities displayed side-by-side on desktop (stacked on mobile, where the expanded detail panel renders inline directly below the active card). All three start collapsed (Ernest, 2026-11-03). Each card shows its number tag ("01 // BUILD"), title, tagline and summary, with no pill in the top-right corner, with the same look and states as the app's other cards (`ChoiceCard`): `--surface-sidebar`, `--surface-hover` on hover, a green edge and glow when open (Ernest, 2026-11-09). Clicking any card expands its full details and interactive simulated widget (clicking an expanded card collapses it; no carousel):
  - **01 Strategy builder:**
    - Tagline: "Define your rules before the market opens"
    - Points: Tested mathematical templates with transparent logic (moving averages, pullbacks, turnarounds); user-controlled stock universes and step budgets; plain language rules with no black-box algorithms.
    - Interactive widget: Template switcher (*Winners at a discount*, *The comeback*, *Bear markets*) updating live trigger criteria and step plans, with watched stock badges (`ZS`, `AAPL`, `MSFT`, `NVDA`) for stock templates and watched fund (`SPY · S&P 500 ETF`) for *Bear markets*.
    - CTA link: "Try building a strategy as guest ›" (`/new`).
  - **02 Real-time simulation:**
    - Tagline: "Forward-test on real prices with zero financial risk"
    - Points: Closing prices refreshed daily after US market close; automated simulated trade logging following exact rules; honest metrics next to S&P 500 fund and worst-year benchmarks.
    - Interactive widget: Forward-testing stats (`+14.2%` vs `+8.1%` S&P 500 fund with `InfoTip` vs `−12.4%` worst year) with toggle between performance comparison and trade log.
    - CTA link: "Test in simulation mode ›" (`/new`).
  - **03 Trading tracker:**
    - Tagline: "Execute at your broker. 100% private & non-custodial"
    - Points: Zero broker sync (no OAuth, passwords, or API access); daily actionable signal alerts; confirm steps with broker fill prices or skip with reasons.
    - Interactive widget: Live signal prompt for Zscaler with interactive action buttons (*I did it*, *I skipped it*, *Remind me tomorrow*) showing immediate tactile feedback and privacy callout.
    - CTA link: "Explore live tracking flow ›" (`/new`).
- **Trust & Architecture section:**
  - Three cards: Zero broker access, Uncompromising benchmark honesty, and Disciplined systematic rules.
- **Bottom CTA banner:**
  - Only displayed for guests (authenticated users do not see this banner).
  - "Ready to test your first strategy? Explore all templates, customize settings, and run simulations with zero commitment. No credit card required."
  - Dual action buttons: "TRY AS GUEST ›" and "Create an account".
- **Footer:** Ends with the standard site footer ("Not financial advice. Signals follow your rules." and the legal links).

### Sign-up (not drawn)

- First name and last name (side by side, required, at most 100 characters each), email and password, with email verification. Accounts from before the names exist have none until they add them in the account ("Name").
- **Password:** at least 10 characters, at most 128. No other rules (no forced symbols or digits). A show/hide toggle. Compared with the email: a password equal to the email is refused.
- Language: English or Catalan, chosen by the user with two buttons. Editable later in the account.
- Zoom: 100% (the default), 110% or 125%, as three buttons, applied at once (see Settings → Zoom).
- Currency: euro (the default), US dollar or British pound, as three buttons (a few short choices look the same on every device, unlike a select's menu). It is the currency of the user's new strategies: budgets, steps and results. Editable in the account; a running strategy keeps the currency it started with. Assumed: three currencies for now, and the same budget limits (500 to 1,000,000) in each.
- No notification choice: every notification is on (see Notifications).
- Time zone, guessed from the browser and editable on Settings. Notifications come at 07:00 in it by default (Settings → Notification time).
- The disclaimer, accepted once: "Not financial advice. Signals follow your rules; you decide and trade at your own broker." Plus one line: the app is for learning, testing and following strategies, and it never trades for you. The button stays off until the box is ticked.
- No push permission and no AI data sharing at sign-up: those are asked when first needed.
- Button: "Create my account". Link: "I already have an account" → Log-in.
- **App start without a session** (built, #31): Sign-up on a device that has never logged in, Log-in on one that has (the device remembers it, nothing secret), and Log-in for any other address the user opened, coming back to it afterwards.
- **On success** the user is logged in at once and goes straight to Pick a strategy template. A "Check your email" notice is shown at the top of the dashboard and new-strategy flow until the email is confirmed ("We sent a link to [email] to confirm it." and "Send it again"); the link itself is only in the email. Until the email is verified the user can use the app, but cannot go live yet: live signals come by email (built, #89; see Choose the mode). Verifying needs no log-in, and the link logs no one in (so it can't be used to log someone into another account): it says "Your email is confirmed" and goes to the dashboard if logged in, else to Log-in. An expired or used link says "This link has expired. Ask for a new one." and goes to the account (which sends it again) if logged in, else to Log-in. Confirming the email address sends a welcome email to new users, linking to the Welcome page ("See how it works") and highlighting app install instructions for iOS and Android with a direct link to the app install section (`/settings#app`).
- **Errors** (under the field): the email is not valid; the password is too short; the disclaimer is not ticked. "That email is already registered" is never shown: the answer to every sign-up looks the same ("Check your email"), and an existing account gets an email saying someone tried to sign up with its address and a link to log in. This way nobody can find out who has an account.
- Too many attempts: "Too many tries. Wait a few minutes and try again."
- **Who can sign up** (built, the invite slice of ROADMAP item 22): the admin setting `SIGN_UPS`, *open* (anyone, the default while the app is built) or *invite only* (from the quiet launch until sign-ups open for the public beta, decided). *Closed* is left out until it is needed (assumed: Ernest asked for invite only or not). An invite has a link (`/sign-up?invite=ABCD-EFGH`) and a code; the invitee opens the link or types the code on Sign-up, in any case, with or without the dash.
  - **From an invite link** the page header is a welcome card instead: "YOU'RE INVITED", "Welcome, [first name]", "[inviter] invited you to Strategy Trading Lab." (or "You have an invite to Strategy Trading Lab." when the inviter has no name), one line on what the app is, and the code. The form is filled in with the invite's first name and language (not its last name or email: anyone holding the code could read them); both can be changed.
  - **A link whose invite can't be used** (used or revoked): "This invite can't be used any more. Ask for a new one, or type another code below." with the code field, empty.
  - **Invite only, without a link:** "Sign-ups are by invitation for now. Enter the code from your invite." above the form, and an "Invite code" field (hint "It is in your invite, like ABCD-EFGH."). Signing up without a code: "Sign-ups are by invitation for now. Enter your invite code." under the field; a wrong, used or revoked one: "This invite code doesn't work. Check it, or ask for a new one."
  - **Open:** the field is always shown, as "Invite code (optional)", so an invited person still gets the vip role. (Ernest, #101: no "I have an invite code" button.)
  - An invite is used once, and only by an account that is created: a sign-up with an email that already has an account answers the same as always and leaves the invite unused. Ernest's invites give the *vip* role; the account records the invite (and so who invited it). No rewards for inviting.
  - Later, with ROADMAP item 37: each vip's own invite link on the account page ("Invite a friend · [n] invites left", 3 uses by default, an admin setting); the people a vip invites get the *user* role and can't invite.
- **"Skip for now, look around"**, under the button: opens Pick a strategy template as a guest (below).

### Looking around without an account (not drawn)

- The start page on a device that has never logged in is Sign-up, with a short intro above the form: what the app is, the two modes, and that it never trades for you or connects to your broker.
- A guest sees every template's review, How we tested and Advanced settings, and can go through the whole new-strategy flow. The draft lives in the browser only, in the tab: a reload anywhere in the flow keeps the chosen template and every choice; closing the tab drops it.
- On Name, preview and confirm, the button reads "Create an account to start it" and opens Sign-up. After sign-up (logged in at once) the user is back on Confirm with the draft, and starts it from there.
- A guest has no dashboard, account or emails. The top bar shows "LOG IN" instead of "ACCOUNT", and the first screen's eyebrow stays "Welcome to Strategy Trading Lab".
- The template endpoints are public already; nothing about a guest is stored on the server.
- A guest's draft is shown in euro and stays in euro after sign-up (the amounts aren't converted). Live can be picked; the email must be confirmed before the strategy starts. A guest never sees another user's draft: a log-out clears it, and so does a reload once the session has ended.
- **When something goes wrong** (item 26): an address the app doesn't have shows "Page not found", "This page doesn't exist, or it moved." and "Go home" (it used to open the home page quietly). A screen that crashes while rendering shows "Something went wrong", "This screen couldn't be shown. Reloading usually fixes it.", "Reload" and "Go home", without the footer; opening another page starts clean. While the device is offline, a line tops every screen: "You're offline. What you see may be out of date." Screens that load data already show their loading, error and not-found states (`LoadableScreen`).
- A screen's one primary action and its note (a short hint or a data source, never a disclaimer) close the page content; there is no separate bottom bar (#105). Every screen ends with a footer: a site map of every page the user may open, then the short disclaimer and the app's version (Ernest, 2026-11-03; the version since #152). Just the links, no headings or brand line, in groups: Welcome, then Templates (a guest) or Dashboard and New strategy; Sign up and Log in (a guest) or Settings, Account and Notifications; for owners only Admin, Alerts, Users, Stocks and funds, Invites and Feedback; and the legal pages (`/legal/notice`, `/legal/terms`, `/legal/privacy`, `/legal/disclaimer`, `/legal/about`). While the account loads, only the legal pages. Pages that need an id (a strategy, a signal) aren't listed. On phones each group's links flow on one line and wrap; from 768px the groups are columns over the whole width. Links stay 44px tall for touch (28px with a mouse from 768px); the current page shows in the primary colour. The pages provide the complete legal notices, terms of use, privacy policy, full disclaimer, and about page with conflicts of interest disclosure. Sign-up records the accepted terms version (`terms_version_accepted`, `terms_accepted_at`).

### Log-in (not drawn)

- H1 "Log in". Fields: email, password (with show/hide). Button "Log in". Links: "Forgot your password?" and "Create an account".
- A wrong email or a wrong password gets the same message: "That email and password don't match." After 5 wrong tries on one email within 15 minutes, that email waits 15 minutes ("Too many tries. Wait a few minutes, or reset your password."). The message is the same for emails that have no account.
- On success: the dashboard, or the screen the user was on when the session ended.
- The session lasts 30 days from the last visit, at most 90 days in all. It is kept on the device, in a cookie the page's scripts can't read. "Log out" (in the account) ends it at once.

### Forgot and reset password (not drawn)

- "Forgot your password?" asks for the email and always answers "If that email has an account, we sent a link. It works for 1 hour." The email has one link to "Choose a new password".
- The reset page has the new password (same rules as sign-up) and the button "Save password". A used or expired link is found out when the password is saved, not when the page opens. On success every session of that user ends, the user is logged in on this device, and goes to the dashboard. An expired or used link says "This link has expired. Ask for a new one." and links to "Forgot your password?".

### Settings (not drawn)

- Reached from "SETTINGS" on the top bar (`/settings`; it was "ACCOUNT" until item 44). How the app behaves for the user; who they are and their data are on Account. In this order:
  - A row with the user's name (or email when they have none) over "Email, name, password and your data", which opens Account.
  - "App": "Install the app", which opens the browser's install prompt when available; otherwise it explains how to add the app from the browser menu, including on iPhone (the PWA includes Android and iPhone home-screen icons).
  - "Language" (two buttons, English and Català, and "Save language"; "Saved.").
  - "Zoom" (three buttons, 100%, 110% and 125%; "Makes everything bigger on this device, like the browser zoom."): the app's own zoom, for people who want larger text. It applies at once, with no save button, and is kept on the device, not the account (assumed: a phone and a desktop often want different sizes, and it then works before log-in too). Also on Sign-up. Built with CSS `zoom` on the page.
  - "Currency" (three buttons, "€ EUR" over "Euro" and so on, and "Save currency"; "Saved. New strategies use it; running ones keep theirs.").
  - "Time zone": a short list, one row per clock, west to east, each with its offset now and a few cities ("GMT+1 · Barcelona, Paris, Copenhagen"; 39 rows, Ernest 2026-11-08: the full IANA list repeated offsets), and the user's own zone when it isn't one of them: on the row that keeps the same clock (Vienna saves as `Europe/Vienna` on Barcelona's row, so no row repeats), or on a row of its own when none does; the same list on Sign-up, then "Your dates and notification time follow it." and "Save time zone" ("Saved."). Spain's row starts with Barcelona (Ernest); the stored value stays the IANA name (`Europe/Madrid`).
  - "Notification time" (item 45, Ernest): "As soon as they're ready" or an hour, 00:00 to 23:00 (07:00 by default), and "Save time" ("Saved."). Under it, for an hour: "Signal emails, the daily summary and reminders come at this time, with the figures from the last US market close."; for as soon as ready: "Signal alerts and the daily summary come right after the US market close is checked. Reminders come at 07:00." See *When notifications come* under Notifications.
  - "Notifications" (see Notifications; reached at `/settings#notifications`): "Signals and reminders", "When a signal, a stock change or an AI round comes up. App notifications come right after the check.", with an "App" and an "Email" box; "Summary", "What changed, on days something did.", with an "Email" box; and, while App is on, "This device": "On for this device." or "Off for this device." with "Turn on" / "Turn off", or why it can't (the browser can't, iPhone needs the home screen first, or notifications are blocked in the browser's settings). Where the app offers no push (no server keys, a browser without push) the App box and *This device* aren't shown.
  - "Send feedback" (see *Feedback* under Admin; reached at `/settings#feedback`).
- An owner gets "ADMIN" on the top bar's right.

### Account (not drawn)

- Reached from Settings' first row (`/account`); the top bar's left is "← SETTINGS". Shows the email (with "Not verified yet" and "Send the link again" when it applies), "Name", for owners "Admin", "Data and privacy" (see below), "Change password" (current password, new password, a "Change password" button; ends the user's other sessions: "Password changed. Your other devices are logged out."), and a "Log out" button, which goes to Log-in. Old links to `/account#notifications` and `/account#feedback` go on to Settings.
- "Delete my account", last: "Deletes your account, strategies, signals and trades, and logs you out everywhere. It can't be undone." and a "Delete my account" button (in the danger colour). It opens in place "Your password", "Delete for good" and "Cancel"; a wrong password says "That isn't your password." On success the app opens the home page as a guest. The API deletes everything about the user (their shared AI rounds too), ends every session and sends one last email, "Your account is deleted" ("We deleted your Strategy Trading Lab account, as you asked. … This is the last email we send you. If this wasn't you, someone had your password: change it wherever else you use it."). Invites they sent and stocks they added stay, without their name; an address on the email block list stays on it. An owner sees "An owner account can't be deleted here." instead (assumed: the app must keep an owner).

### 1 · Pick a strategy template (`Main`)

**Copy**
- Top bar: "DASHBOARD" · "1 / 5"
- Eyebrow: "Welcome to Strategy Trading Lab" (left out when reached from the dashboard)
- H1: "Pick a strategy template"
- Intro: "Each template is a few simple rules with tested defaults. See how it did in the past, tune it, then run it as a simulation or manually. How it works ›" (links to `/welcome?from=new`)
- Button: "Continue"
- Under the button: "Pick one to start. You can add more strategies later, from any template."

**Cards**, in this order. Each shows the name, the tagline in quotes, the idea and a "Learn more" link.

| Name | Tagline | Idea |
| --- | --- | --- |
| Winners at a discount | "Buy the dip" | "A stock on a strong run takes a small step back. The strategy signals that dip." |
| The comeback | "Stocks on the rise" | "A stock that fell a lot starts to recover. The strategy looks for a confirmed turnaround." |
| Bear markets | "When the market is on sale" | "When the whole market falls 20%, the strategy signals buys of a broad S&P 500 fund in small steps as it recovers." |
| Index trend | "Take the wins, dodge the losses" | "The strategy signals a buy of a broad index fund when the market moves into an uptrend, and a sale when the trend turns down." |
| Your AI's top picks | "Let your AI do the reading" | "Every 2 weeks the AI scores each stock from 0 to 100, and the top score should get a position. Use your own AI." |

**Behaviour**
- Single choice. Tapping a card selects it; the selected card is highlighted and shows a filled check, the others an empty box.
- *Winners at a discount* is selected by default, so "Continue" is always enabled.
- "Continue" and "Learn more" both open the review of the selected template.

### 2 · Review a template

**Purpose:** explain the template and let the user accept its prefilled setup. Nothing has to be changed: "Use this template" works straight away.

**Shared layout**, top to bottom:
1. Top bar: "← BACK" (to the pick screen) · "2 / 5".
2. Eyebrow "Strategy template", H1 = template name, tagline in quotes.
3. **"The idea"**: the rationale in plain words, no numbers, no promises.
4. **"It signals when"**: numbered rules (01, 02, 03), each "**Plain label:** detail", with numbers and terms highlighted and explained in brackets.
5. **What it watches**: "Choose stocks to watch" (stock templates) or "The fund it watches" (fund templates).
6. **"Tested"** box: the period and scope line; a small table with columns "This template" and "S&P 500 fund" (with its "i") and rows "A year, on average" `[+X.X%]` (the yearly rate that compounds to the whole period's return) and "Worst year" `[−X%]`; and a "How we tested ›" link to 2c.
7. **"What can go wrong:"** one line.
8. A row "Advanced settings ›" → 2b.
9. Button "Use this template" → Choose the mode.

**Stock list** (stock templates)
- Heading "Choose stocks to watch", counter on the right: "[n] of [total] ticked".
- Each row: a checkbox, the stock's name, one line on what the company does. All ticked by default, alphabetical. Tapping a row ticks or unticks it; unticked names are dimmed.
- Last row: "+ Add a stock" (built, #90; not drawn). It opens the same search as the fund picker under the list ("Search stocks", "Name or symbol"; "Close" folds it), showing stocks only, from the exchanges the app supports (to start, those of the seeded assets: "Stocks from the US, London, Paris, Amsterdam, Copenhagen and Hong Kong exchanges."). Each result shows the name, then "SYMBOL · EXCHANGE · sector · industry". Picking one adds it to the list, ticked, at the end (one already in the list is only ticked again); one the app doesn't have yet is added first, with its price history. Its line under the name is its sector and industry from the price source ("Technology · Software infrastructure"); the seeded stocks keep their written lines. If its price history didn't load when it was added, its row on the review reads "Not enough price history yet."; until its price history is in, its row on the strategy page says the same. The added stocks stay in the new-strategy draft until the strategy starts, and start with it. No result: "Nothing found. Try the company's name or its symbol." A strategy watches at most 60 stocks, the starter list included (the API refuses more: "Use at most 60 stocks."): at 60, the row reads "60 stocks, the most for now" and can't add. Adding a stock the app doesn't have needs a confirmed email ("Confirm your email to add a new stock. Stocks already in the app can be picked now."), and counts toward the user's limit of new stocks and funds (below).
- **Limit on new stocks and funds (built, #90):** a user adds at most `USER_ASSET_LIMIT` stocks and funds the app didn't have (an admin setting, default 30), with "+ Add a stock" and the fund picker together. Picking one already in the app never counts and is never refused. At the limit, adding a new one answers "You can add up to 30 new stocks and funds, and you've reached it. Pick one already in the app." under the search field.
- Adding a stock never needs a deploy: the app looks it up, stores it and fetches its prices on its own (ROADMAP item 12).
- Under the list: "ⓘ In our tests, longer lists gave steadier results."
- The starter lists come from the POC. Zscaler ("Cloud security software") is the only real example in the wireframes.

**Fund block** (fund templates)
- Heading "The fund it watches". One row: the fund's name, a one-line description, and a "Change" link (fund picker, not drawn: the same search as "+ Add a stock", showing funds only; the picked fund replaces the default, and its line is its name).
- **Fund picker (built, assumed details):** "Change" opens a search field ("Search funds", "Name or symbol") under the row; "Close" folds it. Results come after a short pause in typing (2+ characters): the funds already in the app first, then the price source's from the supported exchanges, each with its name and "SYMBOL · EXCHANGE". Picking one that isn't stored adds it with its price history; if the history doesn't load, the row's line reads "Not enough price history yet." A caption names the exchanges ("Funds from the US, London, Paris, Amsterdam, Copenhagen and Hong Kong exchanges."). With a picked fund, a line under the row reads "Tested with [tested fund]. Past results are that fund's." and "Use the tested fund" goes back to it. Confirm's row shows the picked fund. Adding a fund the app doesn't have yet needs a confirmed email (searching and picking a stored fund don't): "Confirm your email to add a new fund. Funds already in the app can be picked now." Errors: "No fund found. Try its symbol.", "Too many searches. Try again in an hour.", the server's reason (not a fund, price source down, the user's limit of new stocks and funds). The results still compare with the index fund (SPY), whatever fund is watched.

**Per-template content**

*Winners at a discount* (`Review`)
- The idea: "Pick winning stocks: companies whose price has climbed steadily for months because the market believes in them. Then wait for a short pullback, when the price dips a few percent."
- It signals when:
  1. "**The long trend is up:** the stock is above its SMA 200 (average price over the last 200 days)."
  2. "**Wait for a discount:** it drops into the discount zone, 5-15% below its recent peak."
  3. "**The dip is not a collapse:** it is still above its SMA 50 (average price over the last 50 days)."
- Tested: "[N] stocks, [year]-[year]."
- What can go wrong: "a stock it buys can keep falling."

*The comeback* (`ReviewComeback`)
- The idea: "A stock that fell a long way starts to climb again. Instead of guessing the bottom, the strategy waits until the turnaround looks confirmed, then signals buys in steps."
- It signals when:
  1. "**It was beaten down:** the stock fell at least [X]% from its high of the last 12 months." (POC: 30% below the 52-week high.)
  2. "**It turned around:** it climbs 15% above its SMA 50 (average price over the last 50 days)."
  3. "**More people are trading it:** its volume (shares traded per day) is rising."
- Tested: "[N] stocks, [year]-[year]."
- What can go wrong: "a recovery can stall, and the stock can fall again."

*Bear markets* (`ReviewDrop`)
- The idea: "Big market crashes are rare and scary, and most people sell. Instead of guessing the bottom, the strategy waits for the first bounce and then signals buys in small steps while prices are still low."
- It signals when:
  1. "**The market fell hard:** the S&P 500 is 20% or more below its high."
  2. "**The first bounce:** it climbs 5% off its low. That's the first step."
  3. "**Then in steps:** one more step every 2 weeks, until the budget is used."
- Fund: "[S&P 500 fund name]" / "Tracks the 500 largest US companies" / "Change".
- Tested: "Major indexes and sector funds, 2000-2024. Big drops are rare: it may not signal for years." (Also added on the strategy page while there are no trades yet.)
- What can go wrong: "the market can keep falling after the first bounce, and stay low for years."

*Index trend* (`ReviewTrend`)
- The idea: "Markets move in long waves. The strategy signals a buy of a broad index fund when the market moves into an uptrend, and a sale when the trend turns down, to dodge the dip. After the dip it looks for a new buy signal."
- It signals when (labelled rows instead of numbers):
  - **BUY**: "The market closes more than 3% above its SMA 200 (average price over the last 200 days)."
  - **SELL**: "The market closes more than 3% below its SMA 200."
  - **ALL**: "All in, all out: each signal covers the whole budget at once."
- Fund: "[Broad index fund name]" / "[What the index covers]" / "Change".
- Tested: "[year]-[year], [N] buys and sells."
- What can go wrong: "when the market zigzags around its average, it can buy and sell often, losing a little each time, or sell just before a rebound."

*Your AI's top picks* (`ReviewAI`). Differs from the shared layout: an "AI" label next to the eyebrow, no Tested box, no How we tested link, and the stock list is a row linking to the list.
- The idea: "Nobody can read every news story and financial report about every company, but an AI can. You run our prompt in the AI you already use; it scores each stock, and the strategy signals the top one. You see whether all that reading turns into better picks."
- It signals when:
  1. "Every [14] days you run the AI round: copy the prompt, paste it into your AI, paste its answer back."
  2. "The stock with the top score gets a signal for one step, until the budget is used."
  3. "If you skip a round, there's no signal that round."
- **"What the AI reads"**: three toggle tiles, all on by default: "News" / "Recent stories", "Mood" / "How people talk about it", "Figures" / "Quarterly numbers". On shows "✓" and a highlight, off shows "+". At least one stays on: the last one on can't be turned off. Use `aria-pressed`. Caption: "At least one stays on. Each one adds a section to the prompt."
- **"The prompt"**, with an "Edit prompt" link. Read-only, shown as code, its first lines with "Show the whole prompt". The API writes it (`POST /templates/ai/prompt`), so the review, the editor and the round always show the same words. It has three parts; only the middle one can be edited:
  1. The date line: "Today is [October 27, 2026]. Search the web for news and data from the last [2 weeks] (the round's frequency: "day", "[5] days", "week", "[2] weeks"), plus each company's latest quarterly figures. Use what you find, not what you remember."
  2. The instructions (suggested, v1): "Score each stock below from 0 to 100 on:", one line per source on ("- News: what recent news says about the business: results, products, contracts, lawsuits, management. 50 is neutral.", "- Mood: how investors, analysts and the press talk about it lately, and whether that tone is getting better or worse. 50 is neutral.", "- Figures: the latest quarterly figures against the quarters and the year before: revenue growth, margins, cash flow and debt."), "and overall, from what you found.", "In the reason, give one line and your main sources, with their dates.", "Score each stock on its own facts. Don't say which stocks to buy or sell."
  3. The answer format and the stocks: "Answer only with this Markdown table (pipes and a dashes row), in a code block so nothing gets changed. One row per stock below, scores as whole numbers from 0 to 100. In the Stock column, copy the stock's symbol exactly as listed below, with no escapes or backslashes.", "| Stock | [sources on] | Overall | Reason |", then "Stocks:" and one line per ticked stock ("- ZS: Zscaler").
  - The comment above it reads "// suggested prompt · v1" or "// your prompt · v1".
  - Caption: "It asks for scores against stated criteria, never which stock to buy."
  - The prompt never includes anything about the user (no budget, goals or holdings). The date window follows the round frequency.
  - The prompt is in English for every user, whatever the app's language, so rounds compare across users and AIs (assumed). The screens around it are translated.
- **Edit prompt** (built, not drawn): "What the AI reads" (the same tiles), "Your instructions" (a text box with the instructions, at most 4,000 characters, hint "Ask for scores against criteria, never which stock to buy."), "⟳ Reset to suggested", and "The whole prompt" as it will be copied. "Save prompt" keeps it in the draft (new strategy) or on the strategy (from the AI round's "Edit prompt", from the next round on). Instructions equal to the suggested ones count as not edited. Intro: "Say what your AI should look at, in your words. The app adds today's date before them, and the answer table and your stocks after them, so it can read the answer."
- Info box: "**What you need:** an AI service that can search the web (most paid plans can). The app doesn't connect to it and never sees your account."
- "**Not tested:** there's no backtest. Results start on the day the strategy starts, and depend on which AI you run it in."
- "**What can go wrong:** your AI can misread the news, or make things up, and a top-scored stock can still fall."
- Rows: "Choose stocks to watch · [n] ticked ›" (opens the stock list) and "Advanced settings · every 14 days · top 1 ›".
- Advanced settings for it (built): "An AI round comes every [n] days." (1 to 60, default 14) and "Each round, one step goes to each of the top [n] by overall score." (1 to 3, default 1), with no backtest section. Rule 02 reads "Each round, one step goes to each of the top `[n]` by overall score, until the budget is used. A stock with all its steps passes its turn to the next."

### 2b · Advanced settings (`Advanced`)

**Purpose:** tune each rule as an exact number, understand what it does, and see the effect on the backtest. The one screen that may be technical: terms like `SMA` are fine here. Each template exposes the POC's numeric rules (the yes/no ones, such as *The comeback*'s "below its SMA 200" and *Bear markets*'s second legs, have no control yet), plus *Winners at a discount*' two averages, which the POC fixes at 200 and 50 days. For *Your AI's top picks* it holds the round frequency (default every 14 days) and how many top-scored stocks get a step per round (default 1), with no backtest.

**Copy**
- Top bar: "← BACK" · "[template name]"
- H1: "Advanced settings"
- Intro: "Every rule as an exact number, next to the tested default." (Shortened while the backtest can't re-run; bring back "Change one and the backtest re-runs" with the re-run.)
- Secondary button: "⟳ Reset to tested"
- Button: "Save settings"

**Groups.** The settings are grouped by what they decide, numbered in the order a stock goes through them, each with a title and a short paragraph that explains that part of the rules: **Candidate** (which stocks it watches; a stock that isn't a candidate gets no signal), **Buy signal** (when a candidate signals), *The comeback*'s **Milestones** (its later steps), and **Pace** (cooldown, or the bear's step interval). *Your AI's top picks* has no groups (one plain list). The texts are in the template's texts (`settingGroups`, and each setting's `help`).

**Setting rows**: the setting as a sentence with the value highlighted, one line on what raising or lowering it does, a − / + stepper on the right, and "Tested: [default]" under it. A changed row is outlined and shows a "CHANGED" label.

*Winners at a discount*:

| Group | Sentence | Default | Step | Min–max |
| --- | --- | --- | --- | --- |
| Candidate | "Proven growth: the recent peak is at least [n]% above the low." | 50% | 5 | 20–200 |
| Candidate | "The low is the lowest close of the last [n] trading days." | 168 | 21 | 63–252 |
| Candidate | "The long average is the SMA over [n] days." | 200 | 10 | 160–300 |
| Buy signal | "The recent peak is the highest close of the last [n] days." | 60 | 5 | 20–60 |
| Buy signal | "The discount zone starts [n]% below the recent peak." | 5% | 1 | 1 – (end − 1), at most 39 |
| Buy signal | "It ends [n]% below the recent peak." | 15% | 1 | (start + 1) – 40 |
| Buy signal | "The floor average is the SMA over [n] days." | 50 | 5 | 20–80 (with the long average at 160 or more, the middle one is always 150 days) |
| Pace | "After a step, wait [n] calendar days before the next buy signal." | 10 | 5 | 0–60 |

*The comeback*:

| Group | Sentence | Default | Step | Min–max |
| --- | --- | --- | --- | --- |
| Candidate | "Beaten down: at least [n]% below its 52-week high." | 30% | 1 | 10–60 |
| Candidate | "It stays a candidate for [n] trading days after it was last beaten down." | 60 | 10 | 0–120 |
| Buy signal | "The buy line is [n]% above the SMA 50." | 15% | 1 | 5–30 |
| Buy signal | "The 5-day average volume is at least [n]× the 50-day." | 1.2× | 0.1 | 1–3 |
| Buy signal | "Chase limit: buy only up to [n]% above the buy line or a milestone's level." | 8% | 1 | 2–20 |
| Milestones | "Milestone near the old high: within [n]% of the 52-week high." | 15% | 1 | 5–30 |
| Milestones | "Milestone breakout: a pullback of at least [n]%, then a close above the high before it." | 8% | 1 | 3–20 |
| Pace | "After a step, wait [n] calendar days before the next buy signal." | 5 | 1 | 0–30 |

*Bear markets*:

| Group | Sentence | Default | Step | Min–max |
| --- | --- | --- | --- | --- |
| Candidate (the drop) | "A drop is at least [n]% below the recent high." | 20% | 5 | 10–40 |
| Candidate (the drop) | "The recent high is the highest close of the last [n] trading days." | 126 | 21 | 63–252 |
| Buy signal | "The first step comes [n]% above the drop's lowest close." | 5% | 1 | 1–15 |
| Pace | "Then one step every [n] trading days." | 10 | 5 | 5–30 |

*Index trend*:

| Group | Sentence | Default | Step | Min–max |
| --- | --- | --- | --- | --- |
| Buy and sell signals | "The trend line is the SMA over [n] days." | 200 | 10 | 50–300 |
| Buy and sell signals | "The buy and sell lines are [n]% above and below the trend line." | 3% | 1 | 0–10 |
| Pace | "After a sale, wait [n] calendar days before the next buy signal." (a sell is never delayed) | 5 | 1 | 0–30 |

The settings are available when starting a strategy and on a running one (Tune the settings).

**"Backtest"** section
- On the defaults: "You're on the tested defaults. Change a setting to compare."
- As soon as any value differs: a table "Period" / "Tested" / "Yours" with rows "[2010-2014]", "[2015-2019]", "[2020-2024]", "All years", and the caption "Several periods, so one lucky stretch doesn't decide."
- Re-run on each change (debounced), with a loading state.
- **Until the backtest engine exists** (built version): the section only says "Backtesting your strategy is coming soon.", on the defaults or not (Ernest, 2026-11-07: the tested defaults' table read as the user's own results). When the re-run comes, keep the index fund and the worst year next to "Yours" (product rules).

**Behaviour**
- "Reset to tested" puts every value back.
- "Save settings" stores the values and returns to the screen that opened it. Leaving with unsaved changes asks first (not drawn, not built yet).
- Each changed row's stepper stops at its range; the zone's start stays at least a step under its end, and the end a step over the start.
- On Review, rules that use settings show the tested defaults or the user's chosen values.
- During the new-strategy flow the values live in the draft; Review's row reads "Advanced settings · tested defaults" or "· [n] changed", and so does Confirm's Settings row. Templates without settings yet show "This template's settings come in a later step." and Confirm leaves out the Settings row.
- Values belong to the user's strategy; the template's defaults never change.

### 2c · How we tested (`HowTested`)

**Copy**
- Top bar: "← BACK" · "[template name]"
- H1: "How we tested"
- Intro: "We ran the template's rules on past prices, day by day, as if it had been running then. Here's exactly how."
- Two columns from 1024px: the main column (left, 3fr) holds **"By period"** and **"How a test runs"** so the results table has full width; the aside column (right, 2fr) holds **"The data"** and the warning box **"What a backtest can't show"**.
- **"By period"**: a table "Period", then "A year, on average" and "Worst year" (each with "Template" and the index name), "Alpha" (with an "i" explaining direct alpha), and an optional "Story" column; rows "2000-12", "2013-19", "2020-26", "All" (standardized across templates from POC research; see `docs/backtest-research-findings.md`). For *Bear markets*, `2013-19` reads "No market crashes". For *Index trend*, tabs above the table switch between the 6 tested indexes (S&P 500 first).
- Caption under table: "All figures are yearly averages. Each period is an independent test that starts with fresh money."
- **"How a test runs"**:
  - "The rules are checked once a day, after the close, as in the app."
  - "Each signal is bought at the next day's opening price."
  - "The tested defaults: €3,000 budget, €100 steps, up to 5 per stock."
  - "Results in euro, at each day's exchange rate."
  - "An S&P 500 fund (SPY) gets the same money on the same days, and is never sold."
  - "Fees: [€X] per trade. Taxes not included."
- **"The data"**: rows "Prices" / "Daily, from [provider]"; "Period" / "[year]-[year]"; "Stocks" / "[N], the starter list" (fund templates: the fund); "Adjusted for" / "Splits and dividends".
- Warning box **"What a backtest can't show"**:
  - "Historical tests use companies and indexes trading today. Failed or delisted companies from decades ago aren't included, which flatters past results."
  - "Real prices can differ from the opening price, especially on busy days."
- Disclaimer at the bottom.

Content is per template (period, scope, figures). *Index trend* is all in, all out, so its "tested defaults" line reads "The whole €3,000 budget, all in, all out." (draft).

### 3 · Choose the mode (`Mode`)

**Copy**
- Top bar: "← BACK" · "3 / 5"
- H1: "How do you want to run it?"
- Intro: "The app never trades or touches your money."
- Under the cards, above the button: "You can start a manual copy of a simulation later."
- Button: "Continue"

**Two option cards**, single choice, *Simulation* selected by default. Each starts with its mode tag and a title (SIM "Simulation", MANUAL "With alerts": the mode is still called Manual everywhere else), then a bold line, then three rows, each a purple label with its text running after it. The two differences that matter are who records the trades and whether each signal sends an alert; neither mode moves money (Ernest, 2026-11-09).

| | Simulation (tag "SIM") | Manual (tag "MANUAL") |
| --- | --- | --- |
| Line | "The app follows the strategy for you." | "Follow the strategy yourself." |
| Alerts: | "none, just a daily summary" | By template: "one for each buy or sell signal" (it sells), "one for each buy signal" (buys only), "a reminder for each AI round" (*Your AI's top picks*) |
| Trades: | "the app records simulated trades" | "you record them in the app" |
| Good for: | "testing a strategy hands-off" | "tracking, with or without real money" |

**Until the email is confirmed** (built, #89): the Manual card can't be picked; under it, "Confirm your email to use Manual." and "Send the link again". A draft that arrives as Manual falls back to Simulation. `POST /strategies` refuses a manual strategy from an unconfirmed account (`errors.mode`).

**No Alerts box** (removed 2026-11-01): notifications are set only in the account, and app notifications are asked for on Confirm (see Notifications).

For *Your AI's top picks*, restate under the cards that it has no backtest (not drawn).

### 4 · Set your budget (`Budget`, drawn in its Live variant)

**Copy**
- Top bar: "← BACK" · "4 / 5"
- Above the H1: the mode tag and the template name.
- H1: "Set your budget"
- Intro, Manual: "The amount you'd invest at your broker. The app doesn't move or hold any money."
- Intro, Simulation (not drawn): "A what-if amount. No real money moves."
- Button: "Continue"
- Under the button: "Amounts in [euro / US dollar / British pound]. Dollar prices convert at the day's rate." (the user's currency)

**Three settings**, each a card with label, big value, − / + stepper and a help line:

| Label | Default | Step | Min–max | Help line |
| --- | --- | --- | --- | --- |
| Budget | €3,000 | €500 | €500–€1,000,000 | "The most this strategy can invest in total." |
| Step size | €100 | €10 | €10–budget | "How much each buy is." |
| Max steps per stock | 5 | 1 | 1–20 | "Buying a stock in steps limits the damage of a bad first buy." |

**Summary box**, updated live:
- "Each buy is [step]. A stock gets at most [max] steps ([step × max]). The budget covers [floor(budget ÷ step)] steps, enough for [floor(steps ÷ max)] stocks bought in full." Singular "step" and "stock" when the number is 1.
- A row of small squares, one per step (at most 60), grouped by stock in alternating shades, captioned "[n] steps in the budget" or "First 60 of [n] steps".
- Last line, Live: "When the budget is used up, the strategy stops signalling buys. Only trades you confirm use the budget; skipped signals don't."
- Last line, Simulation (not drawn): "When the budget is used up, the strategy stops buying."

**Variants (not drawn)**
- *Index trend*: budget only (all in, all out).
- *Bear markets*: one fund, so "Max steps per stock" is the number of steps for the fund (default 5).
- *Your AI's top picks*: add "One step every [14] days uses the budget in about [n] days."

The same three settings can be changed later from the strategy page (not drawn).

### 5 · Name, preview and confirm (`Confirm`, drawn in its Live variant)

**Copy**
- Top bar: "← BACK" · "5 / 5"
- H1: "Ready to start"
- **Name field**, label "Name your strategy". Prefilled with the template's name, numbered if the user already has one by that name ("Winners at a discount 2"). Names are unique among the user's running strategies, ignoring capitals; an archived strategy frees its name. The wireframe shows a user-typed example, "Zscaler & friends".
- Under the field, live preview: "[name] · [template] · [Live|Simulation] · from today"
- **Summary**, one row per item with an "Edit" link:

| Label | Value | Edit → |
| --- | --- | --- |
| Template | "[template name]" | Pick a template |
| Mode | Manual: "Manual: alerts for each signal; you record your trades" (*Your AI's top picks*: "Manual: a reminder for each AI round; you record your trades") · Simulation: "Simulation: the app records simulated trades" | Choose the mode |
| Stocks | "[first stock] and [n] more" (fund templates: the fund's name) | Review |
| Settings | "Tested defaults", or "[n] changed" | Advanced settings |
| Budget | "€[budget] · steps of €[step] · up to [max] per stock" | Set your budget |
| Notifications | Live and AI: how signals and reminders come, from the account: "App and email", "App", "Email" or "Off" (App only where the app offers push). Other simulations: "Summary by email" or "Off". | The account page's Notifications (none for a guest) |
| Prompt (AI only, built) | "Suggested" or "Edited" | Edit prompt |

- **"What happens next"**
  - Live: "You see right away how the template did in the past." / "From today the app watches your stocks and alerts you as soon as your rules are met." / "You confirm each signal, and the app tracks the results of what you did."
  - Simulation (not drawn): "You see right away how the template did in the past." / "From today it runs on real prices and records its own trades." / "A summary comes after the US market close on days something changes."
  - *Your AI's top picks* (not drawn): "There are no past results for this template." / "Your first AI round is due now." / then the mode's line.
- Above the button: nothing; the site footer has the disclaimer.
- Button, Manual: "Start following it". Simulation (not drawn): "Start the simulation".
- The button creates the strategy and opens the dashboard, in place of Confirm in the history, so going back never shows Confirm again. For *Your AI's top picks*, the first AI round waits there under *Waiting for you*.
- While it saves, the button reads "Starting…" and can't be pressed twice. If it fails, the user stays on this screen and sees, under the name: "You already run a strategy with this name. Pick another." when another running strategy took the name, else "We couldn't start it. Try again." (assumed copy)

### Dashboard (`Home`)

**Copy**
- Top bar: "STL // WELCOME" (link to Welcome) · "ACCOUNT" (link to the account page, not drawn)
- H1: "Your strategies"
- Bottom: the site footer (short disclaimer).

**"Waiting for you"** (shown only when something is waiting), with a count badge. One row per item, newest first, each a link:
- Manual signal, tag "MANUAL": "Your rules for **[strategy name]** are met for [stock]. Did you act on it?" → Confirm a signal.
- Live sell signal (not drawn): "Your rules for **[strategy name]** are met for a sale. Did you act on it?"
- AI round due, tag "AI": "Time for the AI round of **[strategy name]**." → AI round.

**Strategy cards**, one per strategy; the whole card links to its strategy page.
- Header: the strategy name; under it "*[template]* · since [date]" or "*[template]* · started today"; on the right the mode tag ("MANUAL" or "SIM"). A small glowing dot before the tag marks a card with something waiting, with an accessible label ("Signal waiting" or "AI round due"). The Waiting for you list carries the detail; the dot only points to the card.
- A fund template names the fund it follows after the template: "*[template]* · [fund symbol] · since [date]".
- The card is short (Ernest, 2026-11-07), with no past results or template text:
  - The gain since start, big, "[+X.X%]": "0.0%", greyed, before the first buy (the money hasn't moved), "–" while bought trades have no price yet. Under it "S&P 500: [+X.X%]" (just "S&P 500" on the card, to keep it short; Ernest, 2026-11-07): with trades, the same money put in on the same days, as on the strategy page; before the first buy, the fund since the start day. Once it has trades, a small line chart of its gain sits on the right.
  - Where the stocks stand, by pill, most pressing first, zeros left out: "[n] signals · [n] getting close · [n] candidates · [n] holding · [n] not a candidate", each with a coloured dot. *Getting close* is a candidate within 3% of its buy rule (the template's *getting close* %, set on `/admin/templates`), not counted again as a candidate (never for *Bear markets*, whose distance is from the low). A fund template says its one fund's state: "[symbol]: candidate". Stocks not checked yet aren't counted; archived cards have no state line.
  - A budget bar and "€[used] of €[budget] used".
- The worst year isn't on the card, an exception to the product rule (Ernest, 2026-11-07): the strategy page keeps it.
- **Archived** (#91, merged with ending in #96): the archived strategies, last archived first, in a collapsed "Archived ([n])" section at the bottom (closed by default), after the running ones and the "+ New strategy" card. Each card carries an "ARCHIVED" tag before the mode tag, "*[template]* · archived [date]" under the name, and its results as they stood on that date. Only running strategies appear in *Waiting for you*.
- Next to the running cards: a dashed placeholder card "+ New strategy" → Pick a template. A user runs at most 10 strategies at once: at 10, the card is off and reads "You run 10 strategies, the most for now. Archive one to start another." (assumed copy)

### Strategy page (`StrategyDetail`, drawn in its Live variant)

**Copy and layout**
- Top bar: "DASHBOARD" · "RENAME" (built #91, not drawn): the heading becomes a "Name" field with "Save" and "Cancel". Confirm's rules: trimmed, 1 to 60 characters, unique among the user's running strategies ignoring capitals; a taken name reads "You already run a strategy with this name. Pick another." under the field. An archived strategy can be renamed too.
- H1: the strategy name; for a single-fund strategy (`investsIn !== 'stockList'`), the fund's latest close (with "Close on [date]" under it) sits on the right of the H1 and the fund's symbol, name, exchange and one-line description sit right below it; under them the mode tag, "Archived on [date]" for an archived strategy, and "*[template]* · since [date]".
- **Summary** (Ernest 2026-11-08: black boxes are only for the charts, the results with their chart and a stock's price chart, so two never stack; the tiles and lists are violet boxes, and a fund's status is the first tile; the page is one column at every width)
  - Tiles come first in every state: a fund's Status (its pill and sentence, on one line when they fit), Budget used, the AI rows and Signals; one per row on a phone, side by side on desktop. Then the framed figures and chart, or, before the first buy, the template's past as quieter text.
  - Live: three figures, "You" `[+X%]`, "The rules alone" `[+X%]`, "S&P 500 fund" `[+X%]` (with its "i"); a chart with three lines (You solid and highlighted, The rules alone solid, S&P 500 fund dashed) and a legend; then "Budget used" / "€[used] of €[budget] · [n] of [total] steps" and "Signals" / "[n] did · [n] skipped · [n] missed".
  - Simulation (not drawn): "Since start" `[+X%]` and "S&P 500 fund" `[+X%]`, a two-line chart, and "Budget used".
  - Under the chart, until the strategy has a year of its own: "Worst year, [years]: the template [−X%], an S&P 500 fund [−X%]" (product rule: results next to the worst year).
  - Before the first buy (not drawn): "No trades yet." and the template's past, a year on average: "In the past, [years], the template did on average [+X.X%] a year vs [+X.X%] for an S&P 500 fund. Its worst year was [−X%], the fund's [−X%]." The chart appears with the first buy.
  - With trades but no price to value them yet (page and dashboard card): "Results show once the prices of your trades are in." A trade without a price or exchange rate is left out of the results until it has one; a buy dated after the index fund's last stored price is compared at that last close.
  - A figure that can't be worked out (the index fund has no prices at all) shows "–".
  - A signal without a step number reads "Signal waiting" / "Bought at the next open". A distance under 1% reads "<1%".
- **Single-fund strategies (`investsIn !== 'stockList'`, *Index trend* and *Bear markets*):** have no "Funds · 1" list and no separate `/strategies/[id]/stocks/[asset]` page (opening that route redirects to `/strategies/[id]`). Instead, the strategy page puts the fund's **Status** first among the Summary's tiles and inlines the **Price chart** and **"Rules on [date]"** from the stock page (without the separate Position section, which would repeat the Summary box), and shows the primary action "Answer the signal" when a live signal is waiting.
- **"Stocks · [n]"** (multi-stock strategies, `investsIn === 'stockList'`), with an **"Edit list"** link on the right that opens the stock list editor (same list as on the review, for this strategy). [n] counts the stocks it follows. A list of more than 7 shows the first 5 (the most pressing first) and "SHOW ALL [n]", which opens the rest ("SHOW FEWER" closes them) (feedback, 2026-11-12).
- **Edit list** (built, ROADMAP item 36; not drawn): `/strategies/{id}/list`, running strategies with a stock list only (a fund template's fund isn't changed while it runs, assumed: the API answers 409, "This strategy's fund can't change while it runs."; opened on an archived or fund strategy, the screen says so instead of the list). Title "Edit list", a short lead ("Unticking a stock stops its signals; waiting ones close as missed. Its trades stay in the results. A stock ticked again starts from today."), then the review's list: "Choose stocks to watch", "[n] of [total] ticked", the stocks it follows ticked and the ones unticked earlier that still have trades unticked, "+ Add a stock" with the same search and limits, and at 60 ticked "60 stocks, the most for now". "Save the list" (off with nothing ticked: "Tick at least one stock to watch.") saves and goes back to the strategy page (`PUT /strategies/{id}/assets`, the full ticked list; the API refuses more than 60, none, or an archived strategy; a stock it follows stays valid after it is delisted). The daily check and the AI round save each stock's signals under the strategy's lock, after checking it still follows the stock, so an unticked stock gets no signal from a check already running. Saving:
  - An unticked stock stops its signals at once: its waiting signals become *Missed*, ending that day (snooze cleared), as when archiving; so does a simulation's buy still waiting for its trade at the next open, which frees its step of the budget. Its trades stay in the results, and a simulation keeps holding what it bought (nothing is sold; open question for Ernest).
  - Its row stays on the page while it has trades in the results, dimmed, with "REMOVED" and "Off the list, no signals", after the other rows; in live mode the rules' own trades keep it too. Without trades it leaves the page (and the editor). A sale by hand can still be logged on it, so the user can record selling what they hold; a buy by hand needs a stock it follows.
  - A stock ticked again (or added) is followed from that moment: the daily check gives it no signal for the days it was off the list (it knows a re-ticked stock by a check on a session before the one last finished when it was added); its first check back looks at the latest finished day only, as for a stock just added (#45). The rules still replay its whole history, so a step the rules took while it was off counts in its step numbers (the known *replay step numbers* issue, ROADMAP *After launch*).
- One row per stock, each a link to that stock's page (not drawn):
  - The name, a status pill and a detail line:
    - "WAITING FOR YOU" (live; "Signal: step [n] of [max]") → Confirm a signal
    - "SIGNAL" (simulation, a signal on the latest checked day whose trade isn't recorded yet; "Step [n] of [max], bought at the next open"). Once the trade is in, the stock reads BUILDING or HOLDING and counts as holding on the dashboard card: an AI simulation's the same day, as it buys at once (F-007).
    - "HOLDING" ("[n] of [max] steps"; also when the rules hold the stock with steps left, as *The comeback* does once its milestones are used, as in the POC: then "[n] of [max] steps · its milestones are used")
    - "BUILDING" ("[n] of [max] steps"; live when it differs: "Rules: [n] of [max] · You: [n] of [max]")
    - "CANDIDATE": the stock meets the template's candidate rules, so the rules watch it for a buy signal. The detail line names the buy rules it still misses, in a few words, from the stock page's checks (at most two, joined by " · "): "Volume 0.84× (needs 1.2×)", "24.7% below its peak (zone 5–15%) · below the floor average". Before the daily check has stored the checks: "[n]% from [the template's zone]" or "Rules not met today". There is no "WATCHING" any more: every active stock of a strategy is watched, so the pill says whether it is a candidate.
    - "NOT A CANDIDATE": it fails a candidate rule, so no signal comes however the price moves. The detail line names the candidate rule it misses ("Rose 31.5% (needs 50%)", "Not beaten down lately"), or the template's words before the checks are stored ("Below its long trend", "Not beaten down"). A signal that ended because the stock stopped being a candidate reads the same.
    - "NOT CHECKED": "Not enough price history yet", or "Not checked yet" before the daily check has a verdict ("Checked after the US session ends" when the check at once couldn't fetch its latest price during a US session).
    - "CANDIDATE" with "No budget left for its steps" when the rules call for steps, or hold steps, the budget had no room for (no signal was made).
    - Rows are ordered: waiting for you, signal, building, holding, watching, then the rest; by name within each.
    - Every row opens the stock's page; a waiting signal is answered from there ("Answer the signal").
    - *Bear markets* shows "NOT A CANDIDATE" with "[n]% below its high (drop at 20%)" in normal markets and "CANDIDATE" with "[n]% above its low (needs 5%)" once a 20% drop is underway, "BUILDING" ("[n] of [max] steps"), and "DONE" ("This drop's steps are bought") once all steps of the drop are bought. Each drop counts only the signals and buys from that drop's first step (`cycleStart`), so a later drop after the budget is used up shows "CANDIDATE" with "No budget left for its steps".
    - *Index trend* shows "CANDIDATE" ("[n]% from the buy line", or "Below its long trend" before the checks are stored) when not holding, like the other templates, and "HOLDING" ("In the market") when holding. (It said "OUT OF THE MARKET" until #156, which read like a verdict on the market next to a fund close to its buy line.)
    - Live, the pill says where the user is, not the rules: when the user skipped every step the rules hold, an all-in or *Index trend* stock is "CANDIDATE" and a stepped one a candidate (or not a candidate), with "Rules: [rules] · You: [you]" under it (e.g. "Rules: In the market · You: Out of the market").
  - On the right: the stock's own result "[+X.X%]" with "You €[invested] in" (simulation: "€[invested] in"), or "–" with "No buys yet".
- **Signals and trades** (`SignalsAndTrades`):
  - A chronological timeline of every signal and trade recorded for this strategy, ordered newest first.
  - Heading: "Signals and trades" with the item count ("· [n]").
  - Live mode action: "+ Log a trade by hand" button that toggles an inline form to record manual buy or sale trades on any of the strategy's active stocks (Stock, Action BUY/SELL, Date, Price, Amount or Shares toggle, Fee, and Exchange rate). Manual trades are marked with a `MANUAL` badge. Manual trades can be dated on any past day up to today (including before the strategy started), but not in the future.
  - In Live mode, each trade row has an "Edit" button to modify its fields (Date, Price, Amount or Shares, Fee, Exchange rate) or "Delete" with inline confirmation to soft-delete the trade (`deleted_at`).
  - Signal rows show: date, stock name and symbol, action (BUY / SELL), status badge (WAITING FOR YOU, COMPLETED, SKIPPED, MISSED, SNOOZED), trigger reason / rule explanation, close price, and link to confirm or inspect the signal.
  - Trade rows show: date, action (BUY / SELL), stock symbol and name, source (`MANUAL` badge if manual), shares count, price, total amount converted to the strategy currency, fee, and exchange rate.
  - Engine invariance: manual trades, edits, and deletions affect only the user's positions and results (`You`), leaving the strategy rules' own position (`The rules alone`) and signal trades unchanged. This also serves as the signal history required by regulation (design rule 7).
- Two buttons: "Tune the settings" / "Change its rules" → Advanced settings; "Backtest" / "How the template did" → Backtest page (not drawn). For AI strategies the second is "AI rounds" (round history, not drawn).
- For running simulations, a third tile: "Follow it manually" / "With these settings" (built, #92; not drawn), see *Following a simulation manually*. A manual copy shows "Copied from [simulation name]" under its name.
- "Change the budget" (budget, step size, max steps per stock; not drawn).
- **No notification switches** (removed 2026-11-01): they are all in the account's Notifications. With signals and reminders off there, a strategy keeps running quietly, and signals left unanswered become *Missed* when they end.
- **One action out of each state** (#96, Ernest's feedback: one word, not *End*, *Archive* and *Delete*): a running strategy can only be archived, and only an archived strategy can be deleted.
- **"Archive the strategy"** (built #91 as *End*, renamed #96, not drawn), running strategies only, with a confirm step in place: "Archive [name]? The daily check stops and signals still waiting close as missed. Its history stays, read-only, under Archived. This can't be undone." with "Archive it" and "Keep it running". Archiving sets it to ended (the date is the user's today), makes every waiting signal *Missed* (ending that day, snooze cleared), skips its AI round due, and frees its name (`POST /strategies/{id}/archive`).
- **An archived strategy** (built #91): read-only. Its results stop at its archive date. No "Tune the settings", no "Log a trade by hand" and no editing or deleting trades, and its missed signals can't be answered any more (the API answers 409, "This strategy is archived, so it can't change."); its answered signals still open. At the bottom: "Delete the strategy". There is no unarchive.
- "Delete the strategy" (built, not drawn), archived strategies only (the API answers 409 for a running one, "Archive the strategy before deleting it."), at the bottom: a confirm step in place, "Delete [name]? Its signals, trades and results go with it. This can't be undone." with "Delete" and "Keep it". It deletes the strategy for good, with its stocks, signals, trades, results and AI rounds, and the rounds it shared (nothing would point to them any more, so turning sharing off couldn't remove them). Then the dashboard, in place of the strategy page in the history.
- Bottom: no extra disclaimer or source note (dropped 2026-11-13; the only disclaimer is in the site footer, #105).

**Metrics**

| Metric | Simulation: from | Live: from |
| --- | --- | --- |
| Budget used | Signal-trade buys, with fees, minus the cost of signal-trade sales, vs budget | Confirmed buys, with fees, minus the cost of confirmed sales, vs budget |
| Average cost | Signal-trade buys | Confirmed buys, with fees |
| Current value | Signal-trade buys, latest price and exchange rate | Confirmed trades, latest price and exchange rate |
| Gain or loss (money and %) | Current value − invested (buys with their fees) | Same |
| Versus the index | Same money (fees included) into a broad index fund (SPY) on the same days, at its opening price | Same, on the confirmed buy days |
| Versus the rules | – | The rules' own position (signal trades) |
| Followed signals | – | Did / skipped / missed |
| Picks vs the rest (AI only) | Top-scored stocks vs the unpicked ones since each round | Same |
| Next up | Stocks closest to their rules, with the distance | Same |

### Stock page (built, not drawn)

One stock under one strategy's rules, at `/strategies/[id]/stocks/[asset]`, opened from its row on the strategy page.

- Top bar: "← STRATEGY" to the strategy page.
- H1: the symbol, with the latest close (the one the rules looked at) next to it as the page's main figure and "Close on [date]" under the price; under them the name and exchange, what it does, and the strategy's mode tag with its name (a link back).
- **Status**, the first tile: the same pill and sentence as its row on the strategy page, on one line when they fit. The candidate date is told once, in "Rules on [date]".
- **Price chart** (under the tiles), not interactive, once the rules have checked the stock (feedback, 2026-11-12: no chart under "Not checked yet"): the daily closes, with a small "90 days", "30 days" (the default) and "7 days" control at its top right, and the rules' levels drawn as they moved day by day, as lines or shaded areas. Every day draws the levels of the stock's verdict today: a candidate's buy area or zone, else the line that would make it one, never both (Ernest, 2026-11-08). A price axis on the right gives the latest close and each level's latest value (both edges of an area), when it shows on the latest day the rules checked, pushed apart so they don't overlap. A legend under the chart names each level.
  - From the strategy's first check of the stock, each day's levels come from the rules replayed from that day, as the daily check does, on today's closes (so a split doesn't leave old levels behind), up to the last day the daily check ran (an archived strategy or an unticked stock stops there). A dashed vertical line marks that day, "Started [date]" (left out when it is before the period shown).
  - Before it, buys don't apply: only the lines that make a stock a candidate show (they go back in time), from the rules run over the price history as for a stock never bought.
  - *The comeback*: a candidate, after the start, the "Buy area" from the buy line to the chase limit; a stock that isn't one, the "Beaten down" line (the 52-week high less the drop the rules need). While the rules hold it, the next milestone's area (the first not used yet), named after its rule ("Back above SMA 200"). While that milestone is the golden cross, no area (it sat under the price and read as a discount) but the "SMA 50" and "SMA 200" lines, on every day (before the start as well), so the cross can be seen coming (Ernest, 2026-11-13).
  - *Winners at a discount*: a candidate, after the start, the "Discount zone"; a stock that isn't one, the "Proven growth" line (its low plus the growth the rules need).
  - *Bear markets*: the "Market fall" line always; after the start, the "Bounce buy" line once a drop has started (none before a drop).
  - *Index trend*: after the start, the "Buy line" and the "Sell line"; before it, the price only.
  - This strategy's trades are dots on the price line on their day, green for a buy and pink for a sell, with "Buy" and "Sell" in the legend.
  - The scale fits the price and every level, so the distance to a far line stays in view.
  - A candidate's chart (the stored verdict, else the latest day's checks) is framed and drawn in green, a stock that isn't one in violet. The chart is left out with fewer than two closes in all; a period with fewer than two (a gap in the prices) shows the last two, its dates under the chart. The data comes from `GET /strategies/{id}/stocks/{assetId}/chart` (the last 90 calendar days).
- **"Rules on [date]"**, the checks of the latest daily check, in parts (a part with no checks is left out):
  - "Is it a candidate?", with "Yes, since [date]: the rules watch it for a buy signal." (or "Yes: the rules watch it for a buy signal." when no date is stored) or "No: it gets no signal, however the price moves."
  - "Buy signal", with "[n] of [m] met. All of them: a buy signal." (not a candidate: "Counts once it is a candidate.", the rules dimmed).
  - "Next step" (*The comeback*'s milestones, *Bear markets*' pace): when the strategy already holds 1+ steps, shown open with "Each new sign earns one more step."; before the first step is bought (`preview: true`), collapsed behind a **"Show next steps"** `<details>` disclosure ("Unlocks after the first step is bought. Each new sign earns one more step."). **Up to date with the trade** (Ernest, 2026-11-12): once the latest check's Manual buy signal is answered (done or skipped), the rules count its step as bought at the next open, so the checks shown are the ones with that step bought, from the last close: the next steps open, the milestones already reached used, and the cooldown. The daily check computes them with the signal (`checksAfterBuy` in the day's facts); the next check replaces them with its own. A Simulation needs none: its trade comes with the next check. *Bear markets* counts its steps itself, so its pace shows after the next check. Held, its first row is "The turn" (step 1): "Step bought", "Step counted at the open on [date]." A milestone that bought a step reads the same; one already reached at a buy reads "Used at a buy", "Already reached at a buy: it can't earn another step." (a buy uses up every sign already there, so the position can reach fewer steps than the budget's maximum). Used rows have no bar. The golden cross before the cross: "Not crossed yet: the SMA 50 ([price]) must rise [n]% to pass the SMA 200 ([price]). Then one step is bought while the price is 0% to +[8]% above the SMA 50 (now up to [price])." with an "SMA 50 [price] ≤ SMA 200 [price]" strip. The breakout before its pullback: "Highest close since the last buy: [price]. A fall to [price] (−[8]%) starts the watch; after it, a close above [price] is a breakout." with a bar of the pullback so far (Ernest, 2026-11-13). While the cooldown after a step runs, a "Cooldown" row says the days left ("[n] more days to wait after the last buy or sale ([m] in all).") with a bar; held, it sits in "Next step" (*The comeback*) or "Buy signal" (*Winners at a discount*). After a sale it sits in "Buy signal", for every template with a cooldown (feedback, 2026-11-12).
  - "Sell signal" (*Index trend* in the market): "Met: a sell signal for the whole position."
  - **Every rule row has a status badge and a visual representation** (no tickless rows):
    - Status badge on the right: `✓ Met`, `○ Not met`, `✓ Step bought`, `✓ Used at a buy`, `✕ Window closed` (when *The comeback*'s candidate window of 60 trading days has expired), `✕ Past zone` (when a *Winners at a discount* pullback fell past the discount zone's max %), or `✕ Past limit` (when price climbed past the chase limit).
    - In *The comeback*, while the stock is a candidate (`beaten` met), today's recovery figures from the 52-week high and vs the SMA 200 are folded into the `Beaten down lately` sentence rather than shown as separate tickless rows; when not a candidate, `Drop from the 52-week high` and `Below SMA 200 at the drop` are shown as ticked candidate checks with threshold bars.
    - Visual gauges per rule type:
      - **Threshold bar (with `> 100%` overflow):** shown whether met or not, with a `100%` target tick at 75% of the bar width so values that exceed the threshold (e.g. `238%` on *Proven growth*) visibly pass the target tick.
      - **3-zone window / band bar (`[Before | Window | Past limit]`):** for *In the discount zone* (`5–15%`) and *The comeback*'s buy line and milestones (`0% to +8%`), showing both the entry threshold and the upper limit past which the signal is cancelled.
      - *Beaten down lately* has no bar: a bar of days read as "more is better". Its sentence says since when it is a candidate, and `Window closed` shows once the window has passed.
      - **Moving-average & level strips:** for *Averages stacked* (`Short $X > Mid $Y > Long $Z`), *Long average rising* (`20d ago $X ↗ Now $Y`), and *Index trend* sell line (`Sell $X · SMA $Y · Buy $Z`). *Index trend* buy line uses a threshold progress bar.
  - The rules' names and sentences are each template's (`checks` in its texts), in English and Catalan.
  - Before the daily check has stored the checks: "Each rule shows here after the next daily check." AI strategies have no rules section.
- **Position** once it has buys: Invested and Result tiles after the Status tile (the status already says the steps held, so there is no Steps tile).
- **Signals and trades** for this stock only; "+ Log a trade by hand" (live) offers only this stock. Rules, stocks and signals are violet boxes of rows; a row's pill or Edit/Delete stays on its first line, at the right.
- A waiting signal (live): the primary action "Answer the signal" opens Confirm a signal.
- Bottom: "Not financial advice. Signals follow your rules."
- A stock that isn't in the strategy: "This stock isn't in this strategy."

### Confirm a signal (`Signal`, live only)

**Copy**
- Top bar: "← STOCK" (the stock's page; a fund strategy's signal: "← STRATEGY") · "SIGNAL · [DATE]"
- Above the H1: the "MANUAL" tag and "[strategy name] · *[template]*"
- H1: the stock's name, a link to its stock page (a stock list's signal)
- Signal box: "Your rules for **[strategy name]** are met for [stock]: [8]% below its recent peak, still above its 50-day average. Under your settings that's step [2] of [5] ([€100])." Then "Close on [date]: $[XXX.XX]". The fact list depends on the template's rules.
- Sell signal for *Index trend* (not drawn): "Your rules for **[strategy name]** are met for a sale: the market closed [3.2]% below its 200-day average."
- **"Did you act on it?"** with two choices side by side: "✓ I did it" and "× I skipped it". "I did it" is preselected in the wireframe.
- A third, quieter action: **Snooze**, "Remind me next trading day", under the form while the signal waits. It doesn't answer the signal; once snoozed it reads "We'll remind you on [date]." 

**I did it**: four fields, each with a visible label, and an exchange-rate line:

| Label | Prefill |
| --- | --- |
| Date | The next trading day |
| Price per share | The next trading day's opening price once it is stored (the daily check). Before that, once that session has opened, the latest price from after the signal day, with that price's day as the date: opening the page fetches it from the price source, not stored (#220; at most one try every 5 minutes per stock, up to 5 seconds), with "Prefilled with the latest price. Change it to your broker's price." Else the signal day's close (feedback, 2026-11-12: typing it in was friction). |
| Amount | The step size minus the tested fee (what buys shares; the step includes its fee). "Shares instead" switches the field to shares ("Amount instead" goes back); sells, all out, are usually entered as shares |
| Fee (optional) | Empty, placeholder "€0.00" |

Exchange-rate line, only when the stock's currency differs from the strategy's: "At $[1.0850] per €1, the rate of [date]. Change". Without a reference rate for the day: "No reference rate for that day yet: enter your broker's." "Change" opens a field for the rate the broker used. The rate follows the date if the date is changed, unless the user entered their own.

Help line under the price: "Prefilled with [date]'s opening price. Change anything that differs from your broker." Before the open is in: "No opening price yet: prefilled with the close on [date]. Change it to your broker's price." Under the amount: "What bought shares. Your step of €[100] includes the fee." The fields show their currency in the label ("Price per share (USD)", "Amount (EUR)"). Button: "Save the trade".

The date is from the signal day to today; a field the server refuses shows why under it ("Enter the price per share.").

**Answered or missed** (not drawn): an answered signal shows the box and "You did it: [n] shares at $[XXX.XX] on [date] (€[amount], fee €[fee])." or "You skipped it: [reason].", with "Back to the stock" ("Back to the strategy" for a fund strategy) instead of the form. A missed signal keeps the form, under "This signal ended on [date] without an answer. You can still record what you did." Its trade counts in budget used like any other, even when later signals used that budget meanwhile, so budget used can read over 100% (open decision, ROADMAP).

**I skipped it**: "Why? (optional)" with single-choice chips "Price ran away", "Didn't trust it", "No cash", "Other". Text: "A skip uses no budget. The rules carry on as if the step was bought, so the next signal for [stock] is step [n+1] of [max]." Button: "Save the skip".

**Snooze**: the signal stays in Waiting for you, and the user is reminded the next US market weekday (New York's date; holidays aren't known yet) from their notification hour (07:00 for as soon as ready) in their time zone, that day or the next (not later) (email "Reminder: [stock], your rules for [strategy]"), once per channel. It can be snoozed again until the signal ends. A missed or answered signal can't be snoozed, nor one whose reminder couldn't be sent (strategy archived; or neither reminder emails, on and with a confirmed address, nor App with a device turned on). Signals from AI rounds cannot be snoozed: they remain due until confirmed, skipped or expired when the next round is due.

**Behaviour**
- Saving records the trade or the skip and goes back to the screen it came from, or up to the stock page (the strategy page for a fund strategy) when opened directly. A trade updates budget used, steps and results.
- Unanswered signals stay in Waiting for you and in the summary. There is no automatic reminder: the user snoozes a signal to be notified again the next day. A signal can be answered later (with the real date). After the signal ends, it is marked *Missed*.
- **Log a trade by hand** on a stock (not drawn): for buys before the alert, or sales for the user's own reasons. Manual trades are marked as such and can be edited or deleted.
- Disclaimer at the bottom.

### AI round (`AIRound`, drawn in its Simulation variant; built, both modes)

**Copy**
- Top bar: "← STRATEGY" · "AI ROUND · [DATE]"
- Above the H1: the mode tag and "[strategy name] · *[template]*"
- H1: "Time for the AI round"
- Intro: "It takes about two minutes. The app doesn't connect to your AI."
- **"01 Copy the prompt"**: a preview of the prompt (filled in with today's date, the ticked stocks and the sources on), cut off after a few lines. A "Copy prompt" button, which turns into "✓ Copied". Under it: "Open in ChatGPT · Claude · Gemini", plain links that open the service in a new tab, with no connection.
- **"02 Paste the answer"**: a text area labelled "Your AI's whole answer", and a select labelled "AI model you used" with "ChatGPT · GPT-5", "Claude · Opus", "Gemini · 2.5 Pro", "Other…" (Other shows a free-text field). Prefilled with the model from the last round.
- **"03 Your AI's scores"**: once the answer is read, one row per stock: overall score, name, the AI's one-line reason. The top score is highlighted. Under it, when the check passes: "✓ Every stock scored, all scores 0-100. The top score gets one step."
- **Consent box**, until the user has answered once (here or in the account), before the button: title "Help us learn which AIs score stocks best", text "Share this round: the AI model you used and its answer, kept apart from your account. We never keep your name, email, budget or trades with it. You can stop at any time in Account → Data and privacy, which also removes what you shared.", checkbox "Share my AI rounds" (off by default), and "What we keep" (opens in place: what a shared round holds, and what it never holds). Using the scores records the answer, ticked or not. Afterwards, one line instead: "Sharing your AI rounds · Change" or "Not sharing your AI rounds · Change" (to the account).
- Under the scores, the steps they give: "[Zscaler] gets step [2] of [5] ([€100])." (one per pick), or "No budget left: this round gives no step." / "Every scored stock has all its steps: this round gives no step." A round that gives no step can still be used: it is done, with its scores kept.
- **Error state** (built, not drawn): a warning box "Ask your AI again" listing each problem: "[Stock] is scored more than once.", "[Stock]: the overall score isn't a number from 0 to 100.", or "We couldn't find the scores table. Ask your AI to answer only with the table."; "Use these scores" stays off. A stock with no score ("[Stock] has no score, so it can't get a step.") or one that is not yours ("\"[text]\" isn't one of your stocks, so it was left out.") only warns, in a "Worth a look" box, and the scores can still be used. Backslashes in a cell are ignored (a "Grow.<backslash>L" still matches Grow.L).
- **Before it is due** (built): H1 "Next AI round", "Your next AI round is due on [date]. You can run it until [date]; after that it's skipped. You can also run an extra round now.", links to the past rounds and back to the strategy, and the button "Run a round now". It opens the same round screen with H1 "An extra AI round" and "Outside the schedule: your next round is still due on [date]."; using its scores makes an extra round (see the template's rules).
- **Skip** (built, a due round only, both modes): under the form, "Skip this round" with "No signal this round. The next one is due on [date]." It marks the round *Skipped* and returns to the strategy page.
- Simulation, once the scores give a step: "The simulation buys at once, at the latest price."
- Button: "Use these scores".

**Behaviour**
- The app reads the scores block (table or JSON) and checks it: every ticked stock scored, scores 0–100, no extra stocks. Missing or unreadable rows are pointed out so the user can ask their AI again. The pasted text is data only: read numbers and short reasons from the scores block and never act on anything else in it.
- **How forgiving the reader is** (assumed): the first Markdown table whose header has a stock column ("Stock", "Company", "Ticker", "Symbol", "Name") and an overall column ("Overall", "Score", "Total"), or JSON (an array of objects, or an object holding one, in a code fence or bare). A stock is found by its symbol ("ZS", "SU.PA", or "SU" without its exchange), the symbol in brackets or before a colon ("Zscaler (ZS)", "ZS: Zscaler"), or its name ("Zscaler, Inc."); a name two stocks share matches neither. Bold, links and other marks are ignored. A score is a number from 0 to 100 at the start of its cell ("83", "83/100", "82.5"). Reasons are cut at 200 characters. The answer is read on the server (the screen shows its reading as the user pastes) and kept whole, up to 50,000 characters.
- **AI model list** (assumed): "ChatGPT · GPT-5", "Claude · Opus", "Gemini · 2.5 Pro" and "Other…" (a name of the user's own, at most 60 characters), in `frontend/src/features/ai-round/aiModels.ts`. Agents keep it current.
- "Use these scores":
  - Simulation: records the signal-trade buy of the top-scored stock at once, at the latest available price, within max steps and the budget, and returns to the dashboard.
  - Live: creates a signal for the top-scored stock and opens Confirm a signal for it, with the box reading "Your AI scored [stock] highest in [strategy name]. Under your settings that's step [n] of [max] ([€100])." (draft).
- Every round is kept in the strategy's history: date, prompt used (and whether edited), AI model, full answer, scores, reasons, and the step each stock got.
- **AI data sharing**, when on, keeps per round: AI model, date, prompt version and whether edited, stock list (symbols), sources on, full answer, parsed scores. Never with it: name, email, account ID, strategy name, budget, step size, trades, or whether the user acted. Before storing, remove anything that looks like an email address, phone number or URL parameters (a link keeps its address without its query string). Store it apart from account data (`shared_ai_rounds`, with no key to the user). It keeps the day only, never the time, so it can't be matched to the user's round by when it was made. It has **no key to the user**; the link is kept on the user's side: their own round notes its shared copy (`ai_rounds.shared_round_id`). It isn't anonymous in the strict sense (its text is the same as the answer the user keeps), so **turning sharing off removes what the user shared**, through those notes. Deleting the account (ROADMAP item 18) must do the same, before it deletes the rounds. A round being saved while the user turns sharing off either sees the new choice or is removed with the rest. "What we keep" says so. (Changed after the code review, #70; the first version claimed it couldn't be traced, which wasn't true.)

### AI rounds (history, built, not drawn)

- Reached from the strategy page's "AI rounds" tile (it replaces "Backtest" for AI strategies) and the AI round screen.
- Top bar "← STRATEGY" · "AI ROUNDS"; H1 "AI rounds"; the strategy's name.
- **"Picks vs the rest"** (once a done round has later prices): "Since each round, the stocks that got a step moved [+x.x%] on average, the others [+y.y%] ([n] rounds)." Caption: "Price changes in each stock's own currency, without fees. It shows whether the scores picked well, not your results." This is the spec's *Picks vs the rest* metric: each done round compares the average change of its picks' closes and of the others' from the round's session to the latest close; the line averages the rounds.
- One card per round, newest first, with its due date and status (DUE, DONE, SKIPPED; an extra round "DONE · EXTRA"): a due one links to the round once it can be run ("Run it now") or reads "The next round."; a skipped one "Skipped: no signal that round."; a done one its AI model and day, "your prompt" when edited, its picks vs the rest, the scores (top first, the picks marked "STEP [n]"), and "The prompt" and "Your AI's answer" opening in place.
- Bottom: the site footer (short disclaimer).

### Strategy page, AI strategies (built)

- Summary box: "No results yet. They start after your first AI round." until a round is done; then as other strategies. Two more rows: "AI round" ("Due now ›" to the round, or "Next on [date]", then "· [n] done · [n] skipped") and, once there are prices since a round, "Picks vs the rest" ("[+x.x%] vs [+y.y%]").
- A stock's row: "SIGNAL" / "WAITING FOR YOU" / "BUILDING" / "HOLDING" as for other templates; otherwise "WATCHING" with "Last score: [n]" (its overall score in the last round that scored it) or "Not scored yet".
- Signals read "Your rules for [strategy] are met for [stock]: your AI scored it [91] out of 100, the top score of the round. Under your settings that's step [n] of [max] ([€100])." (or "number [n] in the round" for a later pick).

### Admin settings (`/admin`, not drawn, for Ernest)

Like the POC's settings page. Its heading is "Admin", the name the account's top bar link and the admin pages' "ADMIN" use too (Ernest, 2026-11-08). Linked from the account's top bar and the footer, for the owner only. Only the **owner** (the account role, below) can open it: a visitor who isn't logged in goes to Log-in and comes back; a user or vip goes to the start page, as if the page weren't there. The API answers 401 and 403 the same way. Besides the settings it has the build, the database in use, a test email, a Prices box (next and last refresh, *Refresh now*, *Test Yahoo*, and the daily schedule), and a link to **Assets** (`/admin/assets`).

- **Roles:** every account has one: *user* (everyone), *vip* (people Ernest invites; they get special privileges later) or *owner* (Ernest). The account's role is in `GET /account/me`. The owner is the account whose verified email is `OWNER_EMAIL` on the server; a vip comes from an owner's invite (or is set in the database).
- **Invites (`/admin/invites`, linked from `/admin` as "Invites · invite people and choose who can sign up"):** for the owner only; English, like the rest of admin.
  - **Who can sign up:** two buttons, *Anyone* and *Invite only*, a line saying what each means, and *Save* (the `SIGN_UPS` setting, also in the settings list on `/admin`).
  - **New invite:** first name (required: the message greets them by it), last name and email (both optional), and the invite's language (*English*, *Català*), which sets the message, the email and the sign-up form's first language. *✉ Send by email* (needs the email) emails it at once, with answers going to the owner; *Make it, copy the text* makes it without an email. At most 30 invite emails an hour.
  - **The invite just made:** a card with "Invite for [name]", the code, the link and the message in the invite's language (greeting by first name, what the app is and that it is in a closed alpha by invitation only, the link, "or sign up at [address] with the code [code]", signed with the owner's first name when the account has one). *Copy message*, *WhatsApp* (opens WhatsApp with the message, to pick who gets it), *Share…* (the phone's share sheet, where the browser has one), *Copy link*, and "+ Another invite".
  - **Sent so far:** the owner's invites, newest first: name, a state (*Not sent*, *Emailed*, *Joined*, *Revoked*), the code, language, date and email, and who joined with it. An unused one has *Copy message*, *Email it* / *Email again* (with an email) and *Revoke* ("Sure? Revoke it"); a revoked invite stops working, and accounts made with it stay.
  - The code is 8 characters in two groups ("ABCD-EFGH") from 31 that can't be mixed up (no 0/O, 1/I/L). It is stored as it is, so the owner can copy the message again. Looking up a code (the welcome on Sign-up) is limited to 30 an hour per client, sign-up to 5; Sign-up without a code isn't limited. An invite whose maker's account was deleted stops working (nobody could revoke it).
- **Management pages and data table:** the admin management pages share one headless table component (`DataTable`, built on TanStack Table and styled with the design tokens): sort by a column, search, dropdown filters, paging ("1–10 of N", "Previous", "Next"), and an action per row; on narrow containers (< 640px) rows turn into cards with no sideways scrolling. Only `/admin` routes load it (and the service worker skips precaching `Admin*` chunks), so regular users never download it.
- **Alerts (`/admin/alerts`, linked first under Management on `/admin` as "Alerts · [n] open", or "none open"; ROADMAP item 21):** what the background jobs found wrong, on a page instead of emails, so Ernest isn't flooded. Raised by: the daily run failing as a whole; an asset's or exchange rate's price fetch failing; the index fund's prices more than one market day behind after a refresh; a strategy that couldn't be checked (an error) or is missing price data (a ticked stock with no prices, a newest price more than 3 market days old, or too little history for its rules); an AI strategy's daily check failing; signal, overview or account emails that couldn't be sent. Each card has a title, the detail (for a strategy: whose it is, its template and mode, then a line per stock), the source, "Since [date time] · last seen [date time] · [n]×", and *Dismiss* while open. Sections: *Open · [n]* ("Nothing open." when empty), *Dismissed · [n]* ("Quiet while the problem lasts. Back only if it clears and happens again.") and *Cleared this week*. Alerts clear themselves when a later run finds the problem gone (the users' email alert when a run's emails all go out and none failed in the last week, the account email alert with the next one that goes out; email not set up while emails are on counts as a failure). No email to the owner (Ernest, item 21).
- **Users (`/admin/users`, linked from `/admin` as "Users · accounts and their strategies"; built #97, read-only):** above the list, the counts: "Accounts" and "Confirmed", and a "Running strategies" table with a row per template (all of them, in the pick screen's order) and its running *Simulation* and *Live* strategies. Then every account in the data table, newest first (`GET /admin/users`): email, role (*User*, *VIP*, *Owner*), "Email confirmed" (*Yes*/*No*), joined and last visit (the last use of any of its sessions, "–" when none is kept) as dates, and its *Running* and *Archived* strategies. Search by email; filters by role and by confirmed email. Test accounts are counted like any other until test invites exist. The list also has each account's name and *Invited by* (the inviter's email, "–" without an invite). Blocking and deleting come with ROADMAP item 37.
- **Templates (`/admin/templates`, linked from `/admin` as "Templates · defaults, starter lists and getting close"):** versions of what a new strategy of each template starts with (Ernest, 2026-11-08), one tab per template in the pick screen's order (the tab shown is kept in the address, `?template=dip`; switching tabs keeps unsaved edits), each with a card headed "v[n] active". **Version**: a dropdown of the template's versions, newest first ("v3 · 2026-11-08 · active · 4 strategies"; version 1 reads "v1 · tested", the catalogue's tested values). Picking one shows its values below. **Defaults**: each Advanced settings rule as a field (its sentence with a blank for the value, "v1 15 · 2 to 40, steps of 1"), checked like Advanced settings. **Getting close**: "within _% of the buy rule", 0.5 to 10 in steps of 0.5, 3 in v1; the active version's sets the dashboard card's *getting close* (never counted for *Bear markets*) and the summary's "comes close" line, for every strategy of the template, running ones too: it is a display threshold, not a rule (Ernest, 2026-11-07). Not for the AI template, whose stocks have no buy rule. **Starter list** (or **Fund** for a fund template): the stocks with × to take one out and the stock search of "+ Add a stock" to add one (or to pick another fund), 1 to 60 stocks, active ones only (a stock already in the version may stay after it was hidden). *Save as new version* saves the values shown as the next version and makes it active (nothing new when they equal the active version); on an older version, *Make v[n] active* makes it active again. "v[n] is active. New strategies start with it." A refused value says why under its field. Versions are never edited or deleted (`template_versions`). A strategy keeps the version it started from: its rules and stocks never change with the template, and the strategy page's "tuned" note compares it with that version, so it reads as tuned only when the user changed something. A new strategy is stamped with the version its screens showed (a tab opened before the owner switched versions keeps its own), and its settings screen shows that version's values as tested. The new-strategy screens present the active version's values as the tested ones ("Tested: 12", "Tested with [fund]"), and past results stay as they are: the owner retunes the tested values on purpose (Ernest, 2026-11-07). The AI template keeps its own starter list, apart from the other two. A stock or fund hidden on `/admin/assets` after it was saved is left out of what new strategies get (all of it gone: v1's list or fund) and shows *hidden*; a saved default the code refuses later (a narrowed range) makes all defaults the catalogue's.
- **Assets (`/admin/assets`, linked from `/admin`):** lists every asset in the database (`GET /admin/assets`) in the data table: asset name, what it does and symbol/exchange/currency, kind (*Stock* or *Fund*), running strategies that list it (ticked or not), latest price date (or "No prices"), status (*Active* or *Hidden*), and *Edit* and *Hide* / *Show* actions per row (`PUT /admin/assets/{id}`, `PUT /admin/assets/{id}/active`). Above the table, **Add an asset**: a Yahoo symbol ("As Yahoo writes it: AAPL, SAP.DE, ^GDAXI.") and *Add* (`POST /admin/assets`). Only the exact symbol is added, and only a stock, fund or index; the app looks up its name, exchange, kind, currency, sector and industry, and fetches its price history (and its currency's exchange rate) at once. It answers "Added Apple Inc. (AAPL), prices to 2026-10-23.", "… is already here." (or, for a hidden one, "… is already here, hidden: Show it to use it."), or why not ("Yahoo has no stock, fund or index with the symbol X."). If the prices don't load, the row is kept and the answer says why; adding it again tries again. *Edit* opens the asset's name and "What it does" (English) above the table, with *Save* and *Cancel*. Prices are fetched daily only for the assets some running strategy lists (ticked or not) and the index fund. When an asset's last price fetch failed (Yahoo's error, or bars that aren't daily), the reason shows under its latest price date until a fetch works.
  - **Prices**, below *Add an asset*: "Years to download" (2 by default, 1 to 30), *Download all* and *Delete all*. *Download all* replaces each active (not hidden) asset's prices with the daily prices of those years (or more, when a strategy started earlier and needs them), one asset at a time ("Downloading 3 of 31: ETN…"), then the exchange rates of every currency in use, and ends with "Downloaded 30 of 31 assets and the exchange rates." plus what failed and why. *Delete all* asks "Sure? Delete all" first, then deletes every price and exchange rate. The *Edit* panel has the same for one asset: its own years, *Download prices* and *Delete prices*. They refuse while the daily refresh or another download runs, and while the API is switched to the other database. The Prices section stays visible while an asset is edited.
- Then "Settings": one box per group (Email, Notifications, Prices, Assets). Every setting but the password has a default (Ernest's Gmail, `smtp.gmail.com`, 587; the sender follows the account), so only the app password has to be typed; it comes last because changing a setting above it unsets it. Each row: the setting's name, what it is for, where the value comes from ("Set here", "From the server's environment", "Default", "Not set"), a field, "Save", and "Default" (back to the server's value or the default). A secret's field is always empty, with "set (hidden): type to replace" or "not set", and "Clear" instead of "Default".
- A refused value says why under the field ("Use a port from 1 to 65535.").
- **Assets** group: `USER_ASSET_LIMIT`, how many new stocks and funds one user can add to the app ("+ Add a stock" and the fund picker; picking one already in the app never counts). Default 30; a whole number, 0 for none ("Use a whole number, e.g. 30."). (#90)
- The saved password only counts while the mail account, server, port and sender stay as they were when it was saved; change any of them (by saving, "Default" or on the server) and it reads "not set"; it counts again only if they go back to the values it was saved with. A password typed while the page shows outdated values is refused: "The mail settings changed since this page loaded. Reload, check them, then type the password again." So it is never sent to a server it wasn't meant for. It is only ever set on this page, never taken from the server's environment.
- Under the Email settings, "Send a test email" sends one email to the signed-in owner's own address (never to another address, so the page can't be used to send mail elsewhere; not to the SMTP login, which a mail service like Brevo gives no inbox), with the settings as saved. It answers "Sent to [address]. Check its inbox." or why not, in the mail server's own words ("…refused it: Username and Password not accepted"). At most 5 an hour.
- Above the settings, **Backend**: the API's version, commit (short) and when it was built ("unknown" in a Debug build), for information.
- Then **Database**: "In use: Dev" or "Production", and one button to use the other ("Use production"). Every request reads and writes the one in use, this page's settings included. The switch is refused, with the reason under the button, unless that database answers and has every migration ("The dev database is missing 2 migration(s): migrate it first."); the deploy migrates production only. While the server uses the database that isn't its own (its own is production on the server, dev locally): "This server normally uses production: switch back when you are done."
- Under it, a row per database: its host (or "Not set") and the server variable its URL comes from ("DATABASE_URL_DEV on the server"), never the rest of the URL, since it holds a password. The URLs are set only on the server (`api.env`, sent by `deploy/backend/deploy-env.ps1`), never on this page, so the page can't point the API at another database. A database whose variable isn't set can't be switched to ("The dev database has no URL: set DATABASE_URL_DEV on the server.").
- The choice lives in a file in the service's state directory, not in the database it chooses. If it can't be read, or the chosen database's variable is gone, the server uses its own database. At every start (so after every deploy) the API checks the database it was switched to again, without delaying the start: if it misses a migration, the API uses its own until the next start (which checks again); if it doesn't answer, it stays, since the page can switch back.
- The Backend and Database boxes don't need the database, so they show even when the settings can't load ("Could not load the settings: the database in use may not answer. Switch above, or try again later."). Only Postgres is switched; object storage stays on the server's own branch.
- The app's version is at the end of the site footer, after the disclaimer, for anyone (moved from the owner-only top bar link in #152, to give the top bar room; owners reach `/admin` from the footer's Admin link). While the API uses the dev database, it shows a **DEV** tag next to the version (from `GET /version`, which says which database is in use).
- Log-in is the app's own. Sessions live in the database in use, so after switching database the owner logs in again there (the account must exist in both).
- **Feedback** (in-app, from selected text, and reviewed on `/feedback`, linked from `/admin`): any signed-in user with a confirmed email can select text on any screen to see a floating `💬 Add comment` button (pinned to the bottom of the screen, or below the sticky header when the selection is near the bottom so it never covers the phone's Copy menu; on desktop, right-clicking a selection also opens `💬 Add comment` / `📋 Copy`). Pressing it opens a modal dialog (`Add a comment`) with a kind picker (`🐞 Bug`, `💡 Idea`, `📝 Note`, `✨ Feature`), the page path, the quoted text and a comment box (`POST /feedback`). Only the **owner** can list, edit, close/reopen or delete feedback (`GET /feedback`, `PATCH /feedback/{id}`, `DELETE /feedback/{id}`), on `/feedback` (linked from the **Management** section on `/admin`), with `Open` / `Closed` / `All` filters, a general comment form and `Copy open as Markdown`. **Send feedback** (#77): a section on the account page (the dashboard's "Send feedback" link, shown with a confirmed email, opens it) with the same kind picker, a comment box and "Send feedback", for a comment not tied to a screen (no page or quote); without a confirmed email it reads "Confirm your email to send feedback." Every new comment, from either place, is emailed to `OWNER_EMAIL` ("New feedback (bug)", the sender in the body, not the subject, which failed sends log) with the sender as Reply-To, so the owner answers by replying; none for the owner's own comments.

## 7. Notifications

**Email layout** (issue #79): every email the app sends (account, notifications, feedback to the owner, the admin test email, invites) uses one layout in the app's cyberpunk style, built by `EmailDocument`: a "STRATEGY TRADING LAB" brand line, a card on the dark ground with a mint top edge, a small uppercase label for the kind of email ("Signal · Manual", "Summary", "Your account", "Invitation · Closed alpha"), then the content, the short disclaimer at the foot of the card wherever signals or results appear, and a footer with "Open the app". The main link is a button (plain-text clients get the address in the multipart text); the overview's sections are lists with *Manual* (pink) and *Simulation* (cyan) tags, and each waiting item links to its page. Invites show a dedicated code badge ("Your code") for manual entry. Each email is sent as HTML and as plain text (multipart/alternative) built from the same blocks, so both say the same thing; clients that don't show HTML show the text. Tables and inline styles only (mail clients drop `<style>`), the text 16px, buttons at least 44px tall, at most 560px wide. Dark only, like the app: assumed, since mail apps that force a light theme may recolour it. The footer doesn't say "you get this because you have an account": a sign-up confirmation can reach someone who never signed up.

**Two kinds, set in one place** (redesigned from scratch, Ernest, 2026-11-01): the user gets fewer options and less explanation, and can't control everything.
- **Signals and reminders**: something needs the user. The live signal alert, the buy signal ended, the snooze reminder and the AI round reminder, by app (web push) and by email; and, by app only, a stock that becomes a candidate or gets close (see *Stock change pushes*).
- **Summary**: nothing to do, just what changed. By email only (Ernest, 2026-11-12: its push is gone, replaced by the stock change pushes).

The **App** box of *Signals and reminders* is the account's one push switch: if a summary push comes back, it follows that box (Ernest, 2026-11-12).

Both are set only on the Settings page's **Notifications** section, for every strategy at once: there is no switch on Choose the mode, on the strategy page or per strategy. A strategy that shouldn't bother the user runs as a simulation, or is archived. With every box off nothing is lost: what waits is still on the dashboard. Everything is on by default, to keep users engaged (Ernest, 2026-11-02): signals and reminders by app and email, the summary by email. Sign-up has no notification choice; the user changes them later in Settings or with an email's unsubscribe link.

**Asking for app notifications** (the browser's permission, needed once per device, and only from a tap): only on Confirm (step 5), for every strategy, simulations too, so the permission is there when their stocks change (Ernest, 2026-11-02). Right above the start button, for a signed-in user where the app offers push:
- the browser hasn't been asked: a checkbox "Allow notifications on this device", **ticked the first time on this device** and unticked once it was put off (remembered in the browser's storage). Start with it ticked opens the browser's prompt first, then starts the strategy whatever the answer. Ticking it also turns the account's App box back on;
- already allowed: no checkbox; Start subscribes the device quietly;
- blocked in the browser: "Notifications are blocked for this app in your browser settings. Alerts still come by email." instead;
- iPhone in Safari, not on the home screen: the home-screen line instead.

There is no ask on the dashboard. Afterwards, Settings → Notifications → *This device*.

**When notifications come** (item 45, Ernest, 2026-11-08): each user picks an hour of their day (07:00 by default) or "As soon as they're ready" on Settings. What a daily check finds by email (live signal alerts, buy signal ended, the summary) waits for the first such hour after the session's bar is final (16:30 in New York), so with 07:00 a session's alerts come the next morning in Europe and the Americas, and the same morning in East Asia; as soon as ready sends them right after the check. Reminders (snoozed signals, AI rounds due) come at that hour of their day; as soon as ready keeps 07:00 for them, since no check makes them. An alert never waits past 2 hours before the next session's close, since that check can end its signal (an hour that would fall later, in a time zone 45 minutes off the hour or just before the close, comes a little early instead). The hourly check sends whatever has come due; a summary whose time comes less than 2 hours before the next session's close, or after it (time zones 45 minutes off the hour), still goes out after that close. Assumed: a signal still waiting is alerted even when its stock moved since; one answered before its time isn't (it no longer waits). **Pushes don't wait** (Ernest, 2026-11-12): a check's pushes (signals, buy signal ended, stock changes) go right after it, whatever the hour, a check run by hand or again included, and a strategy's own check when it starts or its list gains a stock; the hourly check retries what couldn't go out. Reminders come by push at the same hour as their emails.

**Summary** (email and dedicated page, not drawn; the daily overview before 2026-11-01), "Your summary, [date]", sent at the user's notification time once the daily check has evaluated the session (see *When notifications come*), **only on days something changed**: a signal on the session (live or simulated) or a stock change. A quiet day sends nothing, once every strategy the daily check has been checking (any of the last few days) has been checked for the session; then its claim is kept, with "Not sent: nothing changed.", so later runs don't look again. Until then a later run looks again. The hourly check sends it when the user's time comes, and to whoever wasn't sent it then (email not set up yet, a restart, an address just confirmed). Reads like a briefing; never uses buy or sell as an instruction.
1. **Waiting for you**: unanswered live signals and AI rounds due, each with a link.
2. **What changed today** (left out when empty): e.g. "ZS fell 8% from its recent peak and is still above its 50-day average (average price over 50 days). That meets the rules of your strategy *Zscaler & friends*." For simulations, today's signal trades.
3. **Stock changes** (left out when there are none; see below).
4. **Your strategies**: one line per strategy, with its name, mode and result so far.
5. Every item links to its page. The disclaimer at the bottom.

**Daily summary page (`/summary`)**: a dedicated page reached from the footer sitemap (`App` group), titled with the latest evaluated trading day and lead text "Latest trading day, after the US market close." On days with no changes, it shows "No changes" ("Your strategies keep running with no new signals or stock changes."). On days with changes, it shows what is waiting (signals to confirm, AI rounds due), what changed (rules met descriptions with mode tags), stock changes (candidate status and distance with mode tags), and a scoreboard of running strategies (started date, signals count). Top bar links directly to the dashboard.

*Close to the line* is gone: the "comes within 3%" stock change says it once, when it happens, instead of every day.

**Stock changes** (item 29, built; for every running strategy since 2026-11-01, no opt-in): the summary compares each stock's daily check on the session with the trading day before it (the stored verdicts and distance) and adds one line per change, with its mode tag:
- becomes a candidate: "[stock], [strategy]: now a candidate, the rules watch it." (Catalan: "ara és candidata, les regles la vigilen.");
- stops being one: "[stock], [strategy]: no longer a candidate." ("ja no és candidata.");
- comes close: "[stock], [strategy]: now 2.4% below the buy line." (the template's line), when it is a candidate within 3% and wasn't within 3% the day before. Becoming a candidate wins over coming close.

A signal, steps held or too little history aren't changes (the signal has its own lines), nor is a stock without an earlier day (just added). The distance is the template's *getting close* %, 3% unless the owner changed it on `/admin/templates`.

**Stock change pushes** (Ernest, 2026-11-12; replaces the summary push of #213): with the App box on, a stock of a running strategy, Manual or Simulation, that becomes a candidate or comes close (the changes above) is pushed right after the check: "New candidate: [stock]" ("Nova candidata: …") or "Getting close: [stock]" ("A prop de la línia: …"; no *stock* or *fund*, since either can come close), with its stock change line and mode, "(Manual)" or "(Simulation)". A tap opens the stock's page; one stock's later push replaces its earlier one on the device (same tag). Once per stock, kind and session: a check run again on the session, or the next one finding the stock as it was, pushes nothing; it is pushed again only after it changes state again. *No longer a candidate* isn't pushed.

**Live signal alert**: web push right after the daily check that found it, and email at the user's notification time after it (see *When notifications come*). Same text as the signal box. Sent once per channel; a snoozed signal is notified again the next day, once per channel. A stock is alerted at most once in 20 hours per strategy and side: an extra check the same day (Refresh now, a price that came in late, a re-run) that signals the same stock again records that alert as "Not sent: this stock's alert went out today." (Ernest, 2026-11-03). AI strategies do not send signal alerts: an AI round is completed by entering the results directly, and its live signals remain waiting without signal alerts or signal ended notifications.

**Buy signal ended** (Ernest, 2026-11-03): when a live buy signal the user was alerted of ends without an answer because its rules no longer hold (it becomes *Missed*), they are told once per channel, by the channel that alerted them, after the daily check (by push right after it, by email at the user's notification time): "[stock]: the buy signal for [strategy] has ended", with "Since [date] your rules for [strategy] no longer hold for [stock], so this buy signal ended without an answer. You can still record what you did." and the signal box, linking to the signal. The push replaces the signal's own notification on the device (same tag). Not for a signal closed because the strategy was archived or the stock unticked (the user did that), nor for sell signals. Assumed: buy signals only, as asked; sells could follow.

**Sender avatar** (Ernest, 2026-11-03): the round picture mail apps show next to the sender is the mail icon, `frontend/public/icon-mail.svg` (SVG Tiny PS, for BIMI) and `icon-mail.png` (512px): the app icon's background with "STL" only, no frame. Mail apps take it from outside the email (Gmail from the sender's Google account picture, or BIMI), so it is a manual step for Ernest (`TODO.md`).

**Web push** (built, #134; one switch per user since 2026-11-01): signals, stock changes and reminders go to every device the user turned on (the account page's *This device*) while their **App** box is on, whether or not their email is confirmed, and apart from the email boxes. The live signal alert (title: the email's subject, text: the signal box) and the buy signal ended right after the check, the stock change pushes, and the snooze reminder and the AI round reminder at the same times as their emails, once each; the hourly check retries one that couldn't go out. A tap opens the signal (or the round) in the app. A push no device got because a push service failed (5xx, 429, a timeout, or a shutdown mid-send) is tried again by the hourly check, but not one a push service refused (400, 403, 413); a device the push service says is gone, or that has failed for a week, is forgotten. Logging out turns push off on that device, so the next person on it gets none of this account's alerts. Signing in on a device with notifications on moves it to the account signed in (a session that ended without Log out). At most 10 devices per user, the newest kept. A refusal opens a *Web push refused* alert on /admin/alerts (one device: likely gone; all of them: the VAPID keys aren't a pair), cleared by the next delivery. Sent with VAPID keys from the server's environment (`VAPID_PUBLIC_KEY`, `VAPID_PRIVATE_KEY`): without them the app offers no push and only emails. `EMAILS_ENABLED` on the admin page switches push off too.

**AI round due**: notification and email: "Time for the AI round of [strategy name]. It takes about two minutes." (built: once per user and due day, from the user's notification hour (07:00 for as soon as ready) in their time zone, listing every round of theirs due that day, with a link to each; not for a strategy's first round, due the day it starts; by email with a confirmed address, by app on their devices). The summary's *Waiting for you* lists AI rounds due too.

## 8. Data the app needs

The schema is in [backend/DATA-MODEL.md](../backend/DATA-MODEL.md); this is the summary.

- **User:** email, password hash, verified flag, failed log-in count and lock time, sessions and email links (verify, reset), time zone, language (English or Catalan), summary email on/off, signal and reminder emails on/off, push on/off (the one push switch), disclaimer accepted at, push subscriptions, AI data sharing on/off and when changed.
- **Template:** static, in the code, not the database: key, copy (per language), tested defaults with a version number, starter list or fund.
- **Asset:** a stock or fund: symbol, exchange, name, what it does, currency; for added ones, sector and industry from the price source, who added it and when, and whether it is hidden. Seeded or added, it is a plain row: adding one needs no migration or deploy. Prices are fetched daily for the assets some running strategy lists, and for the index fund the results compare with.
- **Invites:** the code, who made it, the role it gives, whom it is for (first name, last name, email, language), uses and the most uses, when it was emailed and revoked; each account records the invite it signed up with.
- **Strategy:** user, template key, the tested-defaults version it started from, name, mode, start date, status (running or ended, which the screens call *archived*; no pause), when it was archived, notifications on/off, currency, budget (budget, step size, max steps per stock; editable), parameter values, and for a live copy the simulation it came from. AI settings (round frequency, top N, sources, the user's instructions) are in the parameters.
- **Strategy stocks:** per strategy, each stock or the fund: active (ticked) or not, added, removed.
- **Signal trades:** per strategy and stock, the trades the rules made, one per signal (date, side, price, shares, amount, fee, exchange rate, step). Drives signals in both modes and the results in simulation.
- **Signals:** strategy, stock, side, step and max steps and step size at the time, the rule facts, the close, date, status (waiting, confirmed, skipped, missed; "did" on screen), skip reason, snoozed until, end date.
- **Notifications:** each one sent, per channel (push, email): kind, the signal it is for, the day, sent or failed. Each goes out once per channel.
- **User trades (live):** strategy, stock, date, side, price, amount and shares (and which one was entered), fee, exchange rate (and whether the user changed it), source (signal or manual), linked signal; manual trades can be deleted.
- **AI rounds:** strategy, due date, status (due, done, skipped), date run, the session its signals are dated, prompt text and version, edited flag, AI model, full answer, parsed scores and reasons, and the step each stock got (`ai_rounds`).
- **Shared AI rounds:** a separate store with no account ID (`shared_ai_rounds`); the user's own round notes its shared copy, to remove it (see the AI round); deleting the strategy removes them too.
- **Daily evaluation:** per strategy and stock: status, distance to the rules, signal start and end.
- **Backtest results:** per template, parameter set and period: result vs the index, worst year, number of trades. Cached by parameter set.
- **Market data:** daily US prices and volumes, exchange rates.
- **Feedback:** author user, kind (`bug` | `idea` | `note` | `feature`), status (`open` | `closed`), comment text, optional page route, DOM selector and selected text quote, timestamps.

## 9. Not designed yet

- ~~The rest of the account page.~~ Delete my account is built (item 18; no data download, Ernest, 2026-11-08). Settings, language, time zone and notifications are designed above; how they work is in [backend/AUTH.md](../backend/AUTH.md). Built: "Data and privacy" with "Share my AI rounds anonymously, to learn which AIs score stocks best." and "What we keep".
- Simulation variants of Set your budget, Confirm and the strategy page. (The AI round's Live variant is built: it opens Confirm a signal for the first pick.)
- Pick screen when adding a strategy (back link, "You run [n] from this template").
- Budget variants for *Index trend* and *Bear markets*.
- Stock page: the chart with the discount zone shaded (Milestone 3), status history.
- Until a screen is built, the controls that open it are hidden, not shown disabled: the "Backtest" tile, and, for a guest, Confirm's "Edit" on the Notifications row. The item that builds the screen brings its control back.
- Sell signals for *Index trend*, and its "Candidate" status when not holding.
- ~~Manual trade form; editing and deleting trades.~~ (built in Signals and trades section, #61)
- The AI comparison page (later: results by AI model, never a ranking of stocks). The round history and the error state are built.
- Backtest page; History (every signal, trade, skip, AI round and status change, in date order).
- Changing the budget.
- Snooze on Confirm a signal; "Enter shares instead" and the exchange-rate line.
- Summary settings (a time of your choice).
- ~~Empty, loading and error states.~~ (built, item 26: a 404 page, a crash page and the offline line; loading and error states per screen)

Needed to publish, and not in this spec yet (proposed in [ROADMAP.md](../ROADMAP.md) → *The launch product*; each gets its section here when it is built):

- "Coming soon" cards on Pick a template for templates the engine can't run yet.
- Live mode gated on a confirmed email (alerts by email, and by web push where the user turned it on, #134).
- ~~Statuses for *Bear markets*; the "Signals and trades" list on the strategy page.~~ (both built: #60, #61)
- ~~Ended strategies.~~ (built #91; since #96 ending is archiving: kept read-only under Archived, and can then be deleted)
- ~~The legal pages' text (legal notice, terms, privacy, About with conflicts of interest); the footer and placeholder pages are built (#100).~~ (built #104)
- Account: "Invite a friend". (Delete my account is built, item 18.)
- ~~Unsubscribe links in emails.~~ (built, item 19): the summary and the signals and reminders (signal alerts, snooze reminders, AI round reminders) carry a "Turn off …" link and the `List-Unsubscribe` one-click headers. The link opens a page with one button (opening a link never changes anything, since mail scanners open every link) that turns that kind off without logging in; an Email box for each kind is on the Settings page's Notifications. Account emails (confirmation, password) have none.
- Admin: users (block, delete), invites and test invites, adding and editing assets, an audit log of owner actions, and feedback.
- Owner access for Claude: an owner account of its own on dev and production, for testing and validation.

## 10. Open questions

- **Regulation.** See `regulation-spain-eu.md`. For the legal review: the taglines "Buy the dip" (an instruction: "Buy") and "Take the wins, dodge the losses" (could read as a promise to avoid losses), the suggested AI prompt, the default stock lists, and the AI comparison page.
- **Anonymous or pseudonymous AI data.** Built with no key to the user, the link kept on the user's own round so turning sharing off removes it (see AI data sharing); Ernest to confirm, and the lawyer to check the consent wording.
- **Edited prompts** in the AI comparison: kept, with the edited flag, so the comparison can keep them apart or leave them out when it is built.
- **The suggested prompt:** v1 written by Claude (`backend/.../Ai/AiPrompt.cs`, versioned by `AiPrompt.Version`); still to test by hand in ChatGPT, Claude and Gemini, and for the legal review.
- **Reading the answer:** settled as above (AI round, "How forgiving the reader is").
- **AI model list:** settled as above; agents keep it current.
- **Price data:** which provider supplies daily US prices and the euro-dollar rate, with terms that allow commercial use, and its cost at many free users. The POC uses eToro and Yahoo.
- **Web push on iPhone:** is a home-screen prompt enough, or do iPhone users rely on email?
- **Card copy for *Your AI's top picks*** on the pick screen: "the top score should get a position" is final as drawn, but "position" is the one trading word there without an explanation.
