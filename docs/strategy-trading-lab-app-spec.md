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

- **Simulation:** the app trades on paper, on real prices, and shows how the strategy does from today. Nothing happens outside the app.
- **Live:** the user follows the strategy with real money at their own broker. The app sends each signal, the user says whether they acted on it or skipped it (or snoozes it until the next day), and the app records those trades and tracks the results.

There is no broker sync in either mode. The app never places orders and never reads the user's broker account; in live mode it knows only what the user confirms. It gives no trading advice.

## 2. Words

| Word | Means |
| --- | --- |
| **Strategy template** | An idea and its rules, as offered in the catalogue, with our tested defaults. Picked, reviewed, backtested and tuned. Templates never change because of what a user does. |
| **Strategy** | The user's own copy of a template: their name for it, their stocks, settings and budget, running in one mode from the day they start it. It keeps its own trades and results. A user can run any number of strategies, several from the same template. |
| **Mode** | *Simulation* or *Live*. Fixed for the life of a strategy; going live means starting a new strategy. |
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
- **No promises.** No claims that markets or stocks will rise or recover. Results are always shown next to the index fund and the worst year.
- **A disclaimer everywhere results or signals appear, in two lengths.** The user sees it over and over, so screens seen again and again (Review, How we tested, the strategy page, Confirm a signal, the footer) use the short one: "Not financial advice. Signals follow your rules." Where the user commits (accepted once at sign-up, above the button on Name, preview and confirm, and the legal page) use the full one: "Not financial advice. Signals follow your rules; you decide and trade at your own broker." The wireframes still show the full one everywhere; this rule wins.
- **No broker sync.** No broker links, no prefilled orders, no reading of the user's account.
- **Plain language.** Real trading terms stay, each explained in brackets the first time it appears, e.g. "SMA 200 (average price over the last 200 days)".
- **Mostly US markets, euro results.** v1 covers the POC's stocks and funds (mostly US-listed, a few listed in Europe and Hong Kong), plus any stock or fund a user adds from the same exchanges. Budgets, steps and results are in euro (`€3,000`), converted at the day's exchange rate; stock prices are shown in the listing's currency (`$412.30`), as at the user's broker. Other currencies may follow; the data already records the currency of every amount.
- **English and Catalan.** v1 ships in both. The user picks the language (nothing is guessed from the browser) and can change it in the account. Emails and notifications follow it. Quoted copy in this spec is the English; the Catalan copy is still to be written.
- **Mode always labelled.** Every strategy shows *Simulation* or *Live*; results of different modes are never mixed.
- **Free for now.** No paywall, plans or pricing.
- **Mobile first.** The wireframes are 390 px wide. Every screen must also work on desktop, with no horizontal scrolling at any width.
- **Functional style rules.** One primary action per screen, at the bottom. Selected items look clearly different from unselected ones. Touch targets at least 44 × 44 px, including − / + steppers. Status is never shown by colour alone: every status has a text label. Text meets WCAG AA contrast.

## 4. Screens and navigation

| Screen | Wireframe file | Reached from | Goes to |
| --- | --- | --- | --- |
| Sign-up (not drawn) | – | App start for new users; "Create an account" on Log-in | Check your email, then Pick a template |
| Log-in (not drawn) | – | App start for returning users; any screen when the session has ended | Dashboard |
| Forgot and reset password (not drawn) | – | "Forgot your password?" on Log-in; the link in the email | Log-in |
| 1 · Pick a strategy template | `Main` | Sign-up; "+ New strategy" on the dashboard | The picked template's review |
| 2 · Review a template (one per template) | `Review`, `ReviewComeback`, `ReviewDrop`, `ReviewTrend`, `ReviewAI` | Pick screen "Continue" or "Learn more"; Edit links on Confirm | Advanced settings, How we tested, Choose the mode |
| 2b · Advanced settings | `Advanced` | Review; "Tune the settings" on the strategy page; Confirm | Back to where it came from |
| 2c · How we tested | `HowTested` | "How we tested ›" on a review (not on the AI review) | Back to that review |
| 3 · Choose the mode | `Mode` | Review "Use this template"; "Go live with these settings" (built, #92; not drawn) | Set your budget |
| 4 · Set your budget | `Budget` | Choose the mode; Confirm | Confirm |
| 5 · Name, preview and confirm | `Confirm` | Set your budget | Dashboard |
| Dashboard (home) | `Home` | Confirm; app start for returning users | Strategy page, Confirm a signal, AI round, Pick a template |
| Strategy page | `StrategyDetail` | A strategy card on the dashboard | Stock page (not drawn), stock list editor, Advanced settings, Backtest (not drawn), Confirm a signal |
| Confirm a signal (live) | `Signal` | Waiting for you on the dashboard; a stock row; a signal alert or the daily overview | Strategy page |
| AI round | `AIRound` | Waiting for you on the dashboard; the AI-round notification | Dashboard (simulation); Confirm a signal (live) |

**New user:** sign-up → pick → review → (advanced settings, how we tested) → mode → budget → confirm → dashboard.

**Adding a strategy:** dashboard "+ New strategy" → the same screens. All templates are shown, with "You run [n] from this template" on templates already in use (not drawn).

**Going live from a simulation (built, #92; not drawn):** "Go live with these settings" on a running simulation's page fills the new-strategy draft from it and opens Choose the mode with Live selected. The draft takes its template, its stocks (starter-list stocks it doesn't watch come unticked, stocks it added come added), its fund, the settings that differ from the tested defaults, its budget, step and max steps, an AI strategy's sources and instructions, and its currency (the copy keeps it, whatever the account's). The name is the simulation's with " live" (Catalan " en directe"), numbered while a running strategy has it ("Zscaler & friends live 2"). Every screen of the flow can still change it. An account without a confirmed email falls back to Simulation on Choose the mode, which says why. `POST /strategies` keeps `sourceStrategyId` for a live strategy only, and refuses one that isn't the user's own simulation (`errors.sourceStrategyId`). The simulation keeps running or can be ended; the two keep separate histories. A live copy's page shows "Copied from [simulation name]", a link to it, while the simulation exists.

**Headers.** Every screen of the new-strategy flow has a top bar with "← BACK" on the left and the step ("1 / 5" to "5 / 5") on the right. The first screen shows "STL // NEW STRATEGY" instead of a back link (when reached from the dashboard, show "← BACK" to the dashboard). Back goes to the previous screen.

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
- **A first check looks at the latest day only.** The first time the daily check runs a strategy's stock, it treats the stock as just added: it evaluates the latest finished day, and signals start from there, with no backlog of past signals (as for a stock added to a running strategy). It matters for strategies that existed before their template's engine ran. Later checks replay the rules from that first day (or from an earlier signal, for a strategy that already had a backlog), so step numbers count only the steps since. The same holds for a new strategy: one started after a day's check has its first check on the next session, and a day the check missed isn't caught up.
- **Signals follow the rules, not the user's trades.** Each strategy keeps the rules' own position (its signal trades), and signals come from it, in both modes. After a skip, the rules carry on as if the step had been bought (the next signal for that stock is the next step), and a *Index trend* sell signal arrives even if the user never bought.
- **Simulation:** the app records every trade the rules call for, at the next trading day's opening price, with the tested fee per trade, about 0.2% of the amount plus 1 in the strategy's currency (€1, $1 or £1; rounded, not converted). The fee is inside the step: amount plus fee is the step (the backtest charged it on top, which changes its results by a fraction of a point). Results come from those signal trades.
- **Live:** the app sends each signal and the user confirms it. Results come only from confirmed trades at the prices the user entered; the rules' own position is shown next to them.
- **A stock the strategy no longer follows** (delisted, unticked or removed): the daily check closes its waiting signals as *Missed*, ending on the last session (#91). It no longer checks the stock; trades in it stay in the results.
- **Budget used** is what the buys cost (with fees) minus the cost of the shares sold since, at average cost: a sale frees budget again. Simulation counts signal trades; live counts only confirmed and hand-logged trades. When the budget is used up, the strategy stops signalling buys.
- **Budget and settings can change while a strategy runs** (strategy page). The new values apply from then on. Lowering the budget below what is already used, or the max steps below a stock's current step, stops further buys; nothing is sold.

## 6. Screen by screen

### Sign-up (not drawn)

- Email and password, with email verification.
- **Password:** at least 10 characters, at most 128. No other rules (no forced symbols or digits). A show/hide toggle. Compared with the email: a password equal to the email is refused.
- Language: English or Catalan, chosen by the user with two buttons. Editable later in the account.
- Currency: euro (the default), US dollar or British pound, as three buttons (a few short choices look the same on every device, unlike a select's menu). It is the currency of the user's new strategies: budgets, steps and results. Editable in the account; a running strategy keeps the currency it started with. Assumed: three currencies for now, and the same budget limits (500 to 1,000,000) in each.
- Daily overview by email, on by default.
- Time zone, guessed from the browser and editable. It sets when reminders arrive (snoozed signals and AI rounds, from 07:00); the overview itself goes out right after the daily check.
- The disclaimer, accepted once: "Not financial advice. Signals follow your rules; you decide and trade at your own broker." Plus one line: the app is for learning, testing and following strategies, and it never trades for you. The button stays off until the box is ticked.
- No push permission and no AI data sharing at sign-up: those are asked when first needed.
- Button: "Create my account". Link: "I already have an account" → Log-in.
- **App start without a session** (built, #31): Sign-up on a device that has never logged in, Log-in on one that has (the device remembers it, nothing secret), and Log-in for any other address the user opened, coming back to it afterwards.
- **On success** the user is logged in at once and goes straight to Pick a strategy template. A "Check your email" notice is shown at the top of the dashboard and new-strategy flow until the email is confirmed ("We sent a link to [email] to confirm it." and "Send it again"); the link itself is only in the email. Until the email is verified the user can use the app, but cannot go live yet: live signals come by email (built, #89; see Choose the mode). Verifying needs no log-in, and the link logs no one in (so it can't be used to log someone into another account): it says "Your email is confirmed" and goes to the dashboard if logged in, else to Log-in. An expired or used link says "This link has expired. Ask for a new one." and goes to the account (which sends it again) if logged in, else to Log-in.
- **Errors** (under the field): the email is not valid; the password is too short; the disclaimer is not ticked. "That email is already registered" is never shown: the answer to every sign-up looks the same ("Check your email"), and an existing account gets an email saying someone tried to sign up with its address and a link to log in. This way nobody can find out who has an account.
- Too many attempts: "Too many tries. Wait a few minutes and try again."
- **Who can sign up** (assumed copy): a setting on the admin page, *open*, *invite only* or *closed*. It stays open while the app is built; from the quiet launch it is invite only, and nobody signs up without an invite until sign-ups open for the public beta (decided). With the gate on, sign-up works only from an invite link; without one, or with a used-up link, the form is replaced by "Sign-ups are by invitation for now." (closed: "Sign-ups are closed for now."). Ernest's own links give the *vip* role. Each vip has their own invite link on the account page ("Invite a friend · [n] invites left"), with 3 uses by default (a setting on the admin page); the people a vip invites get the *user* role and can't invite. The account records who invited it. No rewards for inviting.
- **"Skip for now, look around"**, under the button: opens Pick a strategy template as a guest (below).

### Looking around without an account (not drawn)

- The start page on a device that has never logged in is Sign-up, with a short intro above the form: what the app is, the two modes, and that it never trades for you or connects to your broker.
- A guest sees every template's review, How we tested and Advanced settings, and can go through the whole new-strategy flow. The draft lives in the browser only.
- On Name, preview and confirm, the button reads "Create an account to start it" and opens Sign-up. After sign-up (logged in at once) the user is back on Confirm with the draft, and starts it from there.
- A guest has no dashboard, account or emails. The top bar shows "LOG IN" instead of "ACCOUNT", and the first screen's eyebrow stays "Welcome to Strategy Trading Lab".
- The template endpoints are public already; nothing about a guest is stored on the server.

### Log-in (not drawn)

- H1 "Log in". Fields: email, password (with show/hide). Button "Log in". Links: "Forgot your password?" and "Create an account".
- A wrong email or a wrong password gets the same message: "That email and password don't match." After 5 wrong tries on one email within 15 minutes, that email waits 15 minutes ("Too many tries. Wait a few minutes, or reset your password."). The message is the same for emails that have no account.
- On success: the dashboard, or the screen the user was on when the session ended.
- The session lasts 30 days from the last visit, at most 90 days in all. It is kept on the device, in a cookie the page's scripts can't read. "Log out" (in the account) ends it at once.

### Forgot and reset password (not drawn)

- "Forgot your password?" asks for the email and always answers "If that email has an account, we sent a link. It works for 1 hour." The email has one link to "Choose a new password".
- The reset page has the new password (same rules as sign-up) and the button "Save password". A used or expired link is found out when the password is saved, not when the page opens. On success every session of that user ends, the user is logged in on this device, and goes to the dashboard. An expired or used link says "This link has expired. Ask for a new one." and links to "Forgot your password?".

### Account (first version, not drawn)

- Reached from "ACCOUNT" on the dashboard's top bar. Shows the email (with "Not verified yet" and "Send the link again" when it applies), "App" ("Install the app", which opens the browser's install prompt when available; otherwise it explains how to add the app from the browser menu, including on iPhone; the PWA includes Android and iPhone home-screen icons), "Emails" (the daily overview switch), "Currency" (three buttons, "€ EUR" over "Euro" and so on, and "Save currency"; "Saved. New strategies use it; running ones keep theirs."), "Language" (two buttons, English and Català, and "Save language"; "Saved."), "Send feedback" (see *Feedback* under Admin), "Change password" (current password, new password, a "Change password" button; ends the user's other sessions: "Password changed. Your other devices are logged out."), and a "Log out" button, which goes to Log-in. The rest of the account (time zone, Data and privacy) stays in *Not designed yet*.

### 1 · Pick a strategy template (`Main`)

**Copy**
- Top bar: "STL // NEW STRATEGY" · "1 / 5"
- Eyebrow: "Welcome to Strategy Trading Lab" (left out when reached from the dashboard)
- H1: "Pick a strategy template"
- Intro: "Each template is a few simple rules with tested defaults. See how it did in the past, tune it, then run it as a simulation or live."
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
6. **"Tested"** box: the period and scope line; three figures, "This template" `[+X%]`, "Index fund" `[+X%]`, "Worst year" `[−X%]`; the caption "Past results don't promise future ones."; and a "How we tested ›" link to 2c.
7. **"What can go wrong:"** one line.
8. A row "Advanced settings ›" → 2b.
9. Button "Use this template" → Choose the mode. Under it, the disclaimer.

**Stock list** (stock templates)
- Heading "Choose stocks to watch", counter on the right: "[n] of [total] ticked".
- Each row: a checkbox, the stock's name, one line on what the company does. All ticked by default, alphabetical. Tapping a row ticks or unticks it; unticked names are dimmed.
- Last row: "+ Add a stock" (built, #90; not drawn). It opens the same search as the fund picker under the list ("Search stocks", "Name or symbol"; "Close" folds it), showing stocks only, from the exchanges the app supports (to start, those of the seeded assets: "Stocks from the US, London, Paris, Amsterdam, Copenhagen and Hong Kong exchanges."). Each result shows the name, then "SYMBOL · EXCHANGE · sector · industry". Picking one adds it to the list, ticked, at the end (one already in the list is only ticked again); one the app doesn't have yet is added first, with its price history. Its line under the name is its sector and industry from the price source ("Technology · Software infrastructure"); the seeded stocks keep their written lines. If its price history didn't load when it was added, its row on the review reads "Not enough price history yet."; until its price history is in, its row on the strategy page says the same. The added stocks stay in the new-strategy draft until the strategy starts, and start with it. No result: "Nothing found. Try the company's name or its symbol." A strategy watches at most 60 stocks, the starter list included (the API refuses more: "Use at most 60 stocks."): at 60, the row reads "60 stocks, the most for now" and can't add. Adding a stock the app doesn't have needs a confirmed email ("Confirm your email to add a new stock. Stocks already in the app can be picked now."), and counts toward the user's limit of new stocks and funds (below).
- **Limit on new stocks and funds (built, #90):** a user adds at most `USER_ASSET_LIMIT` stocks and funds the app didn't have (an admin setting, default 30), with "+ Add a stock" and the fund picker together. Picking one already in the app never counts and is never refused. At the limit, adding a new one answers "You can add up to 30 new stocks and funds, and you've reached it. Pick one already in the app." under the search field.
- Adding a stock never needs a deploy: the app looks it up, stores it and fetches its prices on its own (ROADMAP item 13).
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
  1. "**It was beaten down:** the stock fell at least [X]% from its high of the last year." (POC: 30% below the 52-week high.)
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
- Tested: "[N] big drops, [year]-[year]. Big drops are rare: it may not signal for years." (Also added on the dashboard card and strategy page while there are no trades yet.)
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
  3. The answer format and the stocks: "Answer only with this table, one row per stock below, scores as whole numbers from 0 to 100. In the Stock column, write the stock's symbol as listed below.", "| Stock | [sources on] | Overall | Reason |", then "Stocks:" and one line per ticked stock ("- ZS: Zscaler").
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
| Candidate | "Beaten down: at least [n]% below its 52-week high." | 30% | 5 | 10–60 |
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
- As soon as any value differs: a table "Period" / "Tested" / "Yours" with rows "[2010-2014]", "[2015-2019]", "[2020-2024]", "All years", and the caption "Several periods, so one lucky stretch doesn't decide. Past results don't promise future ones."
- Re-run on each change (debounced), with a loading state.
- **Until the backtest engine exists** (built version): with a change, the screen shows the tested results by period (Template, Index, Worst yr, as on How we tested) and "These are the tested defaults' results. Re-running the test with your settings comes later. Past results don't promise future ones." When the re-run comes, keep the index fund and the worst year next to "Yours" (product rules).

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
- **"The data"**: rows "Prices" / "Daily, from [provider]"; "Period" / "[year]-[year]"; "Stocks" / "[N], the starter list" (fund templates: the fund); "Adjusted for" / "Splits and dividends".
- **"How a test runs"**:
  - "The rules are checked once a day, after the close, as in the app."
  - "Each signal is bought at the next day's opening price."
  - "The tested defaults: €3,000 budget, €100 steps, up to 5 per stock."
  - "Results in euro, at each day's exchange rate."
  - "The index fund gets the same money on the same days."
  - "Fees: [€X] per trade. Taxes not included."
- **"By period"**: a table "Period" / "Template" / "Index" / "Worst yr", rows "[2010-14]", "[2015-19]", "[2020-24]", "All".
- Warning box **"What a backtest can't show"**:
  - "The starter list holds companies that are around today. Stocks that failed along the way aren't in it, which flatters past results."
  - "Real prices can differ from the opening price, especially on busy days."
  - "Past results don't promise future ones."
- Disclaimer at the bottom.

Content is per template (period, scope, figures). *Index trend* is all in, all out, so its "tested defaults" line reads "The whole €3,000 budget, all in, all out." (draft).

### 3 · Choose the mode (`Mode`)

**Copy**
- Top bar: "← BACK" · "3 / 5"
- H1: "How do you want to run it?"
- Intro: "You can start a live copy of a simulation later."
- Button: "Continue"

**Two option cards**, single choice, *Simulation* selected by default. Each has a mode tag, the name, a bold line and three rows:

| | Simulation (tag "SIM") | Live (tag "LIVE") |
| --- | --- | --- |
| Line | "Test it on paper. No real money." | "Follow it with real money at your own broker." |
| Who trades: | "the app, on paper, every time the rules say so." | "you, at your broker, if and when you choose." |
| The app: | "records each trade at the next day's opening price." | "emails you each signal and records what you confirm." (until web push: "alerts you to each signal …") |
| You: | "nothing. Read the daily overview." | "confirm each signal: I did it, or I skipped it." |

**Until the email is confirmed** (built, #89): the Live card can't be picked; under it, "Confirm your email to go live." and "Send the link again". A draft that arrives live falls back to Simulation. `POST /strategies` refuses a live strategy from an unconfirmed account (`errors.mode`).

**When Live is selected**
- While web push isn't built (after launch), no Alerts box and no notification prompt: alerts come by email. With web push, an info box "Alerts": "Your browser will ask to allow notifications, so signals reach you right after the market close. Email alerts are on too, as a fallback. On iPhone, add the app to your home screen first."
- Above the button: "The app never trades for you or connects to your broker. It records only what you confirm."
- Request web push permission when the user continues (or on a button in the box). If it's refused, email is the only alert channel.

For *Your AI's top picks*, restate under the cards that it has no backtest (not drawn).

### 4 · Set your budget (`Budget`, drawn in its Live variant)

**Copy**
- Top bar: "← BACK" · "4 / 5"
- Above the H1: the mode tag and the template name.
- H1: "Set your budget"
- Intro, Live: "The amount you plan to put into this strategy at your broker. The app doesn't move or hold any money."
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
- **Name field**, label "Name your strategy". Prefilled with the template's name, numbered if the user already has one by that name ("Winners at a discount 2"). Names are unique among the user's running strategies, ignoring capitals; an ended strategy frees its name. The wireframe shows a user-typed example, "Zscaler & friends".
- Under the field, live preview: "[name] · [template] · [Live|Simulation] · from today"
- **Summary**, one row per item with an "Edit" link:

| Label | Value | Edit → |
| --- | --- | --- |
| Template | "[template name]" | Pick a template |
| Mode | Live: "Live: you trade at your broker and confirm each signal" · Simulation: "Simulation: the app trades on paper" (draft) | Choose the mode |
| Stocks | "[first stock] and [n] more" (fund templates: the fund's name) | Review |
| Settings | "Tested defaults", or "[n] changed" | Advanced settings |
| Budget | "€[budget] · steps of €[step] · up to [max] per stock" | Set your budget |
| Daily overview | "By email, after the US market close" | The account page's Emails section |
| Alerts (Live only) | "Email" ("App and email" once web push is built) | Choose the mode |
| Prompt (AI only, built) | "Suggested" or "Edited" | Edit prompt |

- **"What happens next"**
  - Live: "You see right away how the template did in the past." / "From today the app watches your stocks and alerts you as soon as your rules are met." / "You confirm each signal, and the app tracks the results of what you did."
  - Simulation (not drawn): "You see right away how the template did in the past." / "From today it runs on real prices and records its own paper trades." / "Your first daily overview arrives tomorrow."
  - *Your AI's top picks* (not drawn): "There are no past results for this template." / "Your first AI round is due now." / then the mode's line.
- Above the button: the disclaimer.
- Button, Live: "Start following it live". Simulation (not drawn): "Start the simulation".
- The button creates the strategy and opens the dashboard, in place of Confirm in the history, so going back never shows Confirm again. For *Your AI's top picks*, the first AI round waits there under *Waiting for you*.
- While it saves, the button reads "Starting…" and can't be pressed twice. If it fails, the user stays on this screen and sees, under the name: "You already run a strategy with this name. Pick another." when another running strategy took the name, else "We couldn't start it. Try again." (assumed copy)

### Dashboard (`Home`)

**Copy**
- Top bar: "STL // DASHBOARD" · "ACCOUNT" (link to the account page, not drawn)
- H1: "Your strategies"
- Bottom: the disclaimer and "Past results don't promise future ones." (the cards show results).

**"Waiting for you"** (shown only when something is waiting), with a count badge. One row per item, newest first, each a link:
- Live signal, tag "LIVE": "Your rules for **[strategy name]** are met for [stock]. Did you act on it?" → Confirm a signal.
- Live sell signal (not drawn): "Your rules for **[strategy name]** are met for a sale. Did you act on it?"
- AI round due, tag "AI": "Time for the AI round of **[strategy name]**." → AI round.

**Strategy cards**, one per strategy; the whole card links to its strategy page.
- Header: the strategy name; under it "*[template]* · since [date]" or "*[template]* · started today"; on the right the mode tag ("LIVE" or "SIM"). A small glowing dot before the tag marks a card with something waiting, with an accessible label ("Signal waiting" or "AI round due"). The Waiting for you list carries the detail; the dot only points to the card.
- Running: the result since start, big, "[+X.X%]"; under it "Index fund: [+X.X%]"; a small line chart on the right; a budget bar and "€[used] of €[budget] used".
- Started today: "No paper trades yet. In the past, [year]-[year], the template did [+X%] vs [+X%] for an index fund. Its worst year was [−X%]." (Live: "No trades yet. …", draft.) The worst year is the template's own, from its backtest.
- A running strategy's own figures are since its start, so they have no worst year yet: until it has a year of its own, the card adds "Template's worst year: [−X%]" under the index fund (product rule: results next to the worst year). The page's bottom line carries the caption for them. An empty budget bar, "€0 of €[budget] used".
- AI strategy with no results: "No results yet. They start after your first AI round."
- **Ended strategies** (#91): after the running ones and the "+ New strategy" button, last ended first. The card carries an "ENDED" tag before the mode tag, "*[template]* · ended [date]" under the name, and its results as they stood on its end date.
- **Archived** (#91): the ended strategies the user archived, in a collapsed "Archived ([n])" section at the bottom (closed by default). Only running strategies appear in *Waiting for you*.
- Below the running cards: a dashed button "+ New strategy" → Pick a template. A user runs at most 10 strategies at once: at 10, the button is off and reads "You run 10 strategies, the most for now. End one to start another." (assumed copy)

### Strategy page (`StrategyDetail`, drawn in its Live variant)

**Copy and layout**
- Top bar: "← STRATEGIES" · "RENAME" (built #91, not drawn): the heading becomes a "Name" field with "Save" and "Cancel". Confirm's rules: trimmed, 1 to 60 characters, unique among the user's running strategies ignoring capitals; a taken name reads "You already run a strategy with this name. Pick another." under the field. An ended strategy can be renamed too.
- H1: the strategy name; under it the mode tag, "Ended on [date]" for an ended strategy, and "*[template]* · since [date]".
- **Summary box**
  - Live: three figures, "You" `[+X%]`, "The rules alone" `[+X%]`, "Index fund" `[+X%]`; a chart with three lines (You solid and highlighted, The rules alone solid, Index fund dashed) and a legend; then "Budget used" / "€[used] of €[budget] · [n] of [total] steps" and "Signals" / "[n] did · [n] skipped · [n] missed".
  - Simulation (not drawn): "Since start" `[+X%]` and "Index fund" `[+X%]`, a two-line chart, and "Budget used".
  - Under the chart, until the strategy has a year of its own: "The template's worst year, [years]: [−X%]" (product rule: results next to the worst year).
  - Before the first buy (not drawn): "No trades yet." (simulation: "No paper trades yet.") and the template's past, as on the dashboard card, with its worst year. The chart appears with the first buy.
  - With trades but no price to value them yet (page and dashboard card): "Results show once the prices of your trades are in." A trade without a price or exchange rate is left out of the results until it has one; a buy dated after the index fund's last stored price is compared at that last close.
  - A figure that can't be worked out (the index fund has no prices at all) shows "–".
  - A signal without a step number reads "Signal waiting" / "Bought at the next open". A distance under 1% reads "<1%".
- **"Stocks · [n]"**, with an **"Edit list"** link on the right that opens the stock list editor (same list as on the review, for this strategy).
- One row per stock, each a link to that stock's page (not drawn):
  - The name, a status pill and a detail line:
    - "WAITING FOR YOU" (live; "Signal: step [n] of [max]") → Confirm a signal
    - "SIGNAL" (simulation, a signal on the latest checked day; "Step [n] of [max], bought at the next open")
    - "HOLDING" ("[n] of [max] steps"; also when the rules hold the stock with steps left, as *The comeback* does once its milestones are used, as in the POC: then "[n] of [max] steps · its milestones are used")
    - "BUILDING" ("[n] of [max] steps"; live when it differs: "Rules: [n] of [max] · You: [n] of [max]")
    - "WATCHING" ("[n]% from [the template's zone]", "Rules not met today" when it is there or past it, or, when it isn't a candidate, the template's words: "Below its long trend" for *Winners at a discount*, "Not beaten down" for *The comeback*; "Not enough price history yet" and "Not checked yet" before the daily check has a verdict). The zone is "the discount zone" for *Winners at a discount* and "the buy line" for *The comeback*.
    - "WATCHING" with "No budget left for its steps" when the rules call for steps, or hold steps, the budget had no room for (no signal was made).
    - Rows are ordered: waiting for you, signal, building, holding, watching, then the rest; by name within each.
    - Until the stock page is built, only a waiting row is a link.
    - *Bear markets* shows "WATCHING" with "[n]% below its 6-month high" in normal markets and "In a drop: [n]% above its low" once a 20% drop is underway, "BUILDING" ("[n] of [max] steps"), and "DONE" ("This drop's steps are bought") once all steps of the drop are bought.
    - *Index trend* shows "OUT OF THE MARKET" ("[n]% from the buy line", or "Below its long trend") when not holding, and "HOLDING" ("In the market") when holding.
  - On the right: the stock's own result "[+X.X%]" with "You €[invested] in" (simulation: "€[invested] in"), or "–" with "No buys yet".
- Under the list: "Removing a stock stops its signals. Your trades in it stay in the results."
- **Signals and trades** (`SignalsAndTrades`):
  - A chronological timeline of every signal and trade recorded for this strategy, ordered newest first.
  - Heading: "Signals and trades" with the item count ("· [n]").
  - Live mode action: "+ Log a trade by hand" button that toggles an inline form to record manual buy or sale trades on any of the strategy's active stocks (Stock, Action BUY/SELL, Date, Price, Amount or Shares toggle, Fee, and Exchange rate). Manual trades are marked with a `MANUAL` badge. Manual trades can be dated on any past day up to today (including before the strategy started), but not in the future.
  - In Live mode, each trade row has an "Edit" button to modify its fields (Date, Price, Amount or Shares, Fee, Exchange rate) or "Delete" with inline confirmation to soft-delete the trade (`deleted_at`).
  - Signal rows show: date, stock name and symbol, action (BUY / SELL), status badge (WAITING FOR YOU, COMPLETED, SKIPPED, MISSED, SNOOZED), trigger reason / rule explanation, close price, and link to confirm or inspect the signal.
  - Trade rows show: date, action (BUY / SELL), stock symbol and name, source (`MANUAL` badge if manual), shares count, price, total amount converted to the strategy currency, fee, and exchange rate.
  - Engine invariance: manual trades, edits, and deletions affect only the user's positions and results (`You`), leaving the strategy rules' own position (`The rules alone`) and signal trades unchanged. This also serves as the signal history required by regulation (design rule 7).
- Two buttons: "Tune the settings" / "Change its rules" → Advanced settings; "Backtest" / "How the template did" → Backtest page (not drawn). For AI strategies the second is "AI rounds" (round history, not drawn).
- For running simulations, a third tile: "Go live" / "With these settings" (built, #92; not drawn), see *Going live from a simulation*. A live copy shows "Copied from [simulation name]" under its name.
- "Change the budget" (budget, step size, max steps per stock; not drawn).
- **Alerts switch** (built #91, not drawn), for strategies that email: live ones, "Email me this strategy's signals"; AI simulations, "Email me when its AI round is due"; live AI ones, "Email me its signals and AI rounds". Under it: "The daily overview still lists them." On by default. There is no pause: with alerts off the strategy keeps running quietly, and signals left unanswered become *Missed* when they end. A snooze needs alerts on.
- **"End the strategy"** (built #91, not drawn), running strategies only, with a confirm step in place: "End [name]? The daily check stops and signals still waiting close as missed. Its history stays, read-only. This can't be undone." with "End it" and "Keep it running". Ending sets it to ended (the end date is the user's today), makes every waiting signal *Missed* (ending that day, snooze cleared), skips its AI round due, and frees its name.
- **An ended strategy** (built #91): read-only. Its results stop at its end date. No alerts switch, no "Tune the settings", no "Log a trade by hand" and no editing or deleting trades, and its missed signals can't be answered any more (the API answers 409, "This strategy has ended, so it can't change."); its answered signals still open. At the bottom: "Archive" (or "Unarchive" when archived), then "Delete the strategy".
- "Delete the strategy" (built, not drawn), at the bottom: a confirm step in place, "Delete [name]? Its signals, trades and results go with it. This can't be undone." with "Delete" and "Keep it". It deletes the strategy for good, with its stocks, signals, trades, results and AI rounds, and the rounds it shared (nothing would point to them any more, so turning sharing off couldn't remove them). Then the dashboard, in place of the strategy page in the history. The name is free again.
- Bottom, Live: "Your results come from the trades you confirmed, at your prices. Not financial advice. Signals follow your rules." Simulation (draft): "Results come from paper trades at the next day's opening price. Not financial advice. Signals follow your rules."

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

### Confirm a signal (`Signal`, live only)

**Copy**
- Top bar: "← BACK" · "SIGNAL · [DATE]"
- Above the H1: the "LIVE" tag and "[strategy name] · *[template]*"
- H1: the stock's name
- Signal box: "Your rules for **[strategy name]** are met for [stock]: [8]% below its recent peak, still above its 50-day average. Under your settings that's step [2] of [5] ([€100])." Then "Close on [date]: $[XXX.XX]". The fact list depends on the template's rules.
- Sell signal for *Index trend* (not drawn): "Your rules for **[strategy name]** are met for a sale: the market closed [3.2]% below its 200-day average."
- **"Did you act on it?"** with two choices side by side: "✓ I did it" and "× I skipped it". "I did it" is preselected in the wireframe.
- A third, quieter action: **Snooze**, "Remind me next trading day", under the form while the signal waits. It doesn't answer the signal; once snoozed it reads "We'll remind you on [date]." 

**I did it**: four fields, each with a visible label, and an exchange-rate line:

| Label | Prefill |
| --- | --- |
| Date | The next trading day |
| Price per share | The next trading day's opening price, once the market has opened. Before that, the field is empty and the signal day's close is shown as a reference. |
| Amount | The step size minus the tested fee (what buys shares; the step includes its fee). "Shares instead" switches the field to shares ("Amount instead" goes back); sells, all out, are usually entered as shares |
| Fee (optional) | Empty, placeholder "€0.00" |

Exchange-rate line, only when the stock's currency differs from the strategy's: "At $[1.0850] per €1, the rate of [date]. Change". Without a reference rate for the day: "No reference rate for that day yet: enter your broker's." "Change" opens a field for the rate the broker used. The rate follows the date if the date is changed, unless the user entered their own.

Help line under the price: "Prefilled with [date]'s opening price. Change anything that differs from your broker." Before that price is stored: "The next opening price isn't in yet. The close on [date] was $[XXX.XX]." Under the amount: "What bought shares. Your step of €[100] includes the fee." The fields show their currency in the label ("Price per share (USD)", "Amount (EUR)"). Button: "Save the trade".

The date is from the signal day to today; a field the server refuses shows why under it ("Enter the price per share.").

**Answered or missed** (not drawn): an answered signal shows the box and "You did it: [n] shares at $[XXX.XX] on [date] (€[amount], fee €[fee])." or "You skipped it: [reason].", with "Back to the strategy" instead of the form. A missed signal keeps the form, under "This signal ended on [date] without an answer. You can still record what you did." Its trade counts in budget used like any other, even when later signals used that budget meanwhile, so budget used can read over 100% (open decision, ROADMAP).

**I skipped it**: "Why? (optional)" with single-choice chips "Price ran away", "Didn't trust it", "No cash", "Other". Text: "A skip uses no budget. The rules carry on as if the step was bought, so the next signal for [stock] is step [n+1] of [max]." Button: "Save the skip".

**Snooze**: the signal stays in Waiting for you, and the user is reminded the next US market weekday (New York's date; holidays aren't known yet) from 07:00 in their time zone, that day or the next (not later) (email "Reminder: [stock], your rules for [strategy]"), once per channel. It can be snoozed again until the signal ends. A missed or answered signal can't be snoozed, nor one whose reminder couldn't be sent (strategy ended or its notifications off, email not confirmed).

**Behaviour**
- Saving records the trade or the skip and returns to the strategy page. A trade updates budget used, steps and results.
- Unanswered signals stay in Waiting for you and in the overview. There is no automatic reminder: the user snoozes a signal to be notified again the next day. A signal can be answered later (with the real date). After the signal ends, it is marked *Missed*.
- **Log a trade by hand** on a stock (not drawn): for buys before the alert, or sales for the user's own reasons. Manual trades are marked as such and can be edited or deleted.
- Disclaimer at the bottom.

### AI round (`AIRound`, drawn in its Simulation variant; built, both modes)

**Copy**
- Top bar: "← BACK" · "AI ROUND · [DATE]"
- Above the H1: the mode tag and "[strategy name] · *[template]*"
- H1: "Time for the AI round"
- Intro: "It takes about two minutes. The app doesn't connect to your AI."
- **"01 Copy the prompt"**: a preview of the prompt (filled in with today's date, the ticked stocks and the sources on), cut off after a few lines. A "Copy prompt" button, which turns into "✓ Copied". Under it: "Open in ChatGPT · Claude · Gemini", plain links that open the service in a new tab, with no connection.
- **"02 Paste the answer"**: a text area labelled "Your AI's whole answer", and a select labelled "AI model you used" with "ChatGPT · GPT-5", "Claude · Opus", "Gemini · 2.5 Pro", "Other…" (Other shows a free-text field). Prefilled with the model from the last round.
- **"03 Your AI's scores"**: once the answer is read, one row per stock: overall score, name, the AI's one-line reason. The top score is highlighted. Under it, when the check passes: "✓ Every stock scored, all scores 0-100. The top score gets one step."
- **Consent box**, until the user has answered once (here or in the account), before the button: title "Help us learn which AIs score stocks best", text "Share this round: the AI model you used and its answer, kept apart from your account. We never keep your name, email, budget or trades with it. You can stop at any time in Account → Data and privacy, which also removes what you shared.", checkbox "Share my AI rounds" (off by default), and "What we keep" (opens in place: what a shared round holds, and what it never holds). Using the scores records the answer, ticked or not. Afterwards, one line instead: "Sharing your AI rounds · Change" or "Not sharing your AI rounds · Change" (to the account).
- Under the scores, the steps they give: "[Zscaler] gets step [2] of [5] ([€100])." (one per pick), or "No budget left: this round gives no step." / "Every scored stock has all its steps: this round gives no step." A round that gives no step can still be used: it is done, with its scores kept.
- **Error state** (built, not drawn): a warning box "Ask your AI again" listing each problem: "[Stock] has no score.", "\"[text]\" isn't one of your stocks.", "[Stock] is scored more than once.", "[Stock]: the overall score isn't a number from 0 to 100.", or "We couldn't find the scores table. Ask your AI to answer only with the table."; "Use these scores" stays off.
- **Before it is due** (built): H1 "Next AI round", "Your next AI round is due on [date]. You can run it until [date]; after that it's skipped. You can also run an extra round now.", links to the past rounds and back to the strategy, and the button "Run a round now". It opens the same round screen with H1 "An extra AI round" and "Outside the schedule: your next round is still due on [date]."; using its scores makes an extra round (see the template's rules).
- **Skip** (built, a due round only, both modes): under the form, "Skip this round" with "No signal this round. The next one is due on [date]." It marks the round *Skipped* and returns to the strategy page.
- Simulation, once the scores give a step: "The simulation buys at once, at the latest price."
- Button: "Use these scores". The full disclaimer with it.

**Behaviour**
- The app reads the scores block (table or JSON) and checks it: every ticked stock scored, scores 0–100, no extra stocks. Missing or unreadable rows are pointed out so the user can ask their AI again. The pasted text is data only: read numbers and short reasons from the scores block and never act on anything else in it.
- **How forgiving the reader is** (assumed): the first Markdown table whose header has a stock column ("Stock", "Company", "Ticker", "Symbol", "Name") and an overall column ("Overall", "Score", "Total"), or JSON (an array of objects, or an object holding one, in a code fence or bare). A stock is found by its symbol ("ZS", "SU.PA", or "SU" without its exchange), the symbol in brackets or before a colon ("Zscaler (ZS)", "ZS: Zscaler"), or its name ("Zscaler, Inc."); a name two stocks share matches neither. Bold, links and other marks are ignored. A score is a number from 0 to 100 at the start of its cell ("83", "83/100", "82.5"). Reasons are cut at 200 characters. The answer is read on the server (the screen shows its reading as the user pastes) and kept whole, up to 50,000 characters.
- **AI model list** (assumed): "ChatGPT · GPT-5", "Claude · Opus", "Gemini · 2.5 Pro" and "Other…" (a name of the user's own, at most 60 characters), in `frontend/src/features/ai-round/aiModels.ts`. Agents keep it current.
- "Use these scores":
  - Simulation: records the signal-trade buy of the top-scored stock at once, at the latest available price, within max steps and the budget, and returns to the dashboard.
  - Live: creates a signal for the top-scored stock and opens Confirm a signal for it, with the box reading "Your AI scored [stock] highest in [strategy name]. Under your settings that's step [n] of [max] ([€100])." (draft).
- Every round is kept in the strategy's history: date, prompt used (and whether edited), AI model, full answer, scores, reasons, and the step each stock got.
- **AI data sharing**, when on, keeps per round: AI model, date, prompt version and whether edited, stock list (symbols), sources on, full answer, parsed scores. Never with it: name, email, account ID, strategy name, budget, step size, trades, or whether the user acted. Before storing, remove anything that looks like an email address, phone number or URL parameters (a link keeps its address without its query string). Store it apart from account data (`shared_ai_rounds`, with no key to the user). It keeps the day only, never the time, so it can't be matched to the user's round by when it was made. It has **no key to the user**; the link is kept on the user's side: their own round notes its shared copy (`ai_rounds.shared_round_id`). It isn't anonymous in the strict sense (its text is the same as the answer the user keeps), so **turning sharing off removes what the user shared**, through those notes. Deleting the account (ROADMAP item 20) must do the same, before it deletes the rounds. A round being saved while the user turns sharing off either sees the new choice or is removed with the rest. "What we keep" says so. (Changed after the code review, #70; the first version claimed it couldn't be traced, which wasn't true.)

### AI rounds (history, built, not drawn)

- Reached from the strategy page's "AI rounds" tile (it replaces "Backtest" for AI strategies) and the AI round screen.
- Top bar "← BACK" · "AI ROUNDS"; H1 "AI rounds"; the strategy's name.
- **"Picks vs the rest"** (once a done round has later prices): "Since each round, the stocks that got a step moved [+x.x%] on average, the others [+y.y%] ([n] rounds)." Caption: "Price changes in each stock's own currency, without fees. It shows whether the scores picked well, not your results." This is the spec's *Picks vs the rest* metric: each done round compares the average change of its picks' closes and of the others' from the round's session to the latest close; the line averages the rounds.
- One card per round, newest first, with its due date and status (DUE, DONE, SKIPPED; an extra round "DONE · EXTRA"): a due one links to the round once it can be run ("Run it now") or reads "The next round."; a skipped one "Skipped: no signal that round."; a done one its AI model and day, "your prompt" when edited, its picks vs the rest, the scores (top first, the picks marked "STEP [n]"), and "The prompt" and "Your AI's answer" opening in place.
- Bottom: the short disclaimer and "Past results don't promise future ones."

### Strategy page, AI strategies (built)

- Summary box: "No results yet. They start after your first AI round." until a round is done; then as other strategies. Two more rows: "AI round" ("Due now ›" to the round, or "Next on [date]", then "· [n] done · [n] skipped") and, once there are prices since a round, "Picks vs the rest" ("[+x.x%] vs [+y.y%]").
- A stock's row: "SIGNAL" / "WAITING FOR YOU" / "BUILDING" / "HOLDING" as for other templates; otherwise "WATCHING" with "Last score: [n]" (its overall score in the last round that scored it) or "Not scored yet".
- Signals read "Your rules for [strategy] are met for [stock]: your AI scored it [91] out of 100, the top score of the round. Under your settings that's step [n] of [max] ([€100])." (or "number [n] in the round" for a later pick).

### Admin settings (`/admin`, not drawn, for Ernest)

Like the POC's settings page. Not linked from the app; reached by its address. Only the **owner** (the account role, below) can open it: a visitor who isn't logged in goes to Log-in and comes back; a user or vip goes to the start page, as if the page weren't there. The API answers 401 and 403 the same way. Besides the settings it has the build, the database in use, a test email, a Prices box (next and last refresh, *Refresh now*, *Test Yahoo*, and the daily schedule), and a link to **Assets** (`/admin/assets`).

- **Roles:** every account has one: *user* (everyone), *vip* (people Ernest invites; they get special privileges later, and there is no invitation yet) or *owner* (Ernest). The account's role is in `GET /account/me`. The owner is the account whose verified email is `OWNER_EMAIL` on the server; a vip is set in the database for now.
- **Management pages and data table:** the admin management pages share one headless table component (`DataTable`, built on TanStack Table and styled with the design tokens): sort by a column, search, dropdown filters, paging ("1–10 of N", "Previous", "Next"), and an action per row; on narrow containers (< 640px) rows turn into cards with no sideways scrolling. Only `/admin` routes load it (and the service worker skips precaching `Admin*` chunks), so regular users never download it.
- **Assets (`/admin/assets`, linked from `/admin`):** lists every asset in the database (`GET /admin/assets`) in the data table: asset name, what it does and symbol/exchange/currency, kind (*Stock* or *Fund*), running strategies that list it (ticked or not), latest price date (or "No prices"), status (*Active* or *Hidden*), and *Edit* and *Hide* / *Show* actions per row (`PUT /admin/assets/{id}`, `PUT /admin/assets/{id}/active`). Above the table, **Add an asset**: a Yahoo symbol ("As Yahoo writes it: AAPL, SAP.DE, ^GDAXI.") and *Add* (`POST /admin/assets`). Only the exact symbol is added, and only a stock, fund or index; the app looks up its name, exchange, kind, currency, sector and industry, and fetches its price history (and its currency's exchange rate) at once. It answers "Added Apple Inc. (AAPL), prices to 2026-10-23.", "… is already here." (or, for a hidden one, "… is already here, hidden: Show it to use it."), or why not ("Yahoo has no stock, fund or index with the symbol X."). If the prices don't load, the row is kept and the answer says why; adding it again tries again. *Edit* opens the asset's name and "What it does" (English) above the table, with *Save* and *Cancel*. Prices are fetched daily only for the assets some running strategy lists (ticked or not) and the index fund.
- Then "Settings": one box per group (Email, Notifications, Prices, Assets). Every setting but the password has a default (Ernest's Gmail, `smtp.gmail.com`, 587; the sender follows the account), so only the app password has to be typed; it comes last because changing a setting above it unsets it. Each row: the setting's name, what it is for, where the value comes from ("Set here", "From the server's environment", "Default", "Not set"), a field, "Save", and "Default" (back to the server's value or the default). A secret's field is always empty, with "set (hidden): type to replace" or "not set", and "Clear" instead of "Default".
- A refused value says why under the field ("Use a port from 1 to 65535.").
- **Assets** group: `USER_ASSET_LIMIT`, how many new stocks and funds one user can add to the app ("+ Add a stock" and the fund picker; picking one already in the app never counts). Default 30; a whole number, 0 for none ("Use a whole number, e.g. 30."). (#90)
- The saved password only counts while the mail account, server, port and sender stay as they were when it was saved; change any of them (by saving, "Default" or on the server) and it reads "not set"; it counts again only if they go back to the values it was saved with. A password typed while the page shows outdated values is refused: "The mail settings changed since this page loaded. Reload, check them, then type the password again." So it is never sent to a server it wasn't meant for. It is only ever set on this page, never taken from the server's environment.
- Under the Email settings, "Send a test email" sends one email to the mail account itself (never to another address, so the page can't be used to send mail elsewhere), with the settings as saved. It answers "Sent to [account]. Check its inbox." or why not, in the mail server's own words ("…refused it: Username and Password not accepted"). At most 5 an hour.
- Above the settings, **Backend**: the API's version, commit (short) and when it was built ("unknown" in a Debug build), for information.
- Then **Database**: "In use: Dev" or "Production", and one button to use the other ("Use production"). Every request reads and writes the one in use, this page's settings included. The switch is refused, with the reason under the button, unless that database answers and has every migration ("The dev database is missing 2 migration(s): migrate it first."); the deploy migrates production only. While the server uses the database that isn't its own (its own is production on the server, dev locally): "This server normally uses production: switch back when you are done."
- Under it, a row per database: its host (or "Not set") and the server variable its URL comes from ("DATABASE_URL_DEV on the server"), never the rest of the URL, since it holds a password. The URLs are set only on the server (`api.env`, sent by `deploy/backend/deploy-env.ps1`), never on this page, so the page can't point the API at another database. A database whose variable isn't set can't be switched to ("The dev database has no URL: set DATABASE_URL_DEV on the server.").
- The choice lives in a file in the service's state directory, not in the database it chooses. If it can't be read, or the chosen database's variable is gone, the server uses its own database. At every start (so after every deploy) the API checks the database it was switched to again, without delaying the start: if it misses a migration, the API uses its own until the next start (which checks again); if it doesn't answer, it stays, since the page can switch back.
- The Backend and Database boxes don't need the database, so they show even when the settings can't load ("Could not load the settings: the database in use may not answer. Switch above, or try again later."). Only Postgres is switched; object storage stays on the server's own branch.
- While the API uses the dev database, every screen's top bar shows a **DEV** tag next to the version (from `GET /version`, which says which database is in use).
- Log-in is the app's own. Sessions live in the database in use, so after switching database the owner logs in again there (the account must exist in both).
- **Feedback** (in-app, from selected text, and reviewed on `/admin` and `/feedback`): any signed-in user with a confirmed email can select text on any screen to see a floating `💬 Add comment` button (pinned to the bottom of the screen, or below the sticky header when the selection is near the bottom so it never covers the phone's Copy menu; on desktop, right-clicking a selection also opens `💬 Add comment` / `📋 Copy`). Pressing it opens a modal dialog (`Add a comment`) with a kind picker (`🐞 Bug`, `💡 Idea`, `📝 Note`, `✨ Feature`), the page path, the quoted text and a comment box (`POST /feedback`). Only the **owner** can list, edit, close/reopen or delete feedback (`GET /feedback`, `PATCH /feedback/{id}`, `DELETE /feedback/{id}`), in the **Feedback** section on `/admin` and on `/feedback`, with `Open` / `Closed` / `All` filters, a general comment form and `Copy open as Markdown`. **Send feedback** (#77): a section on the account page (the dashboard's "Send feedback" link, shown with a confirmed email, opens it) with the same kind picker, a comment box and "Send feedback", for a comment not tied to a screen (no page or quote); without a confirmed email it reads "Confirm your email to send feedback." Every new comment, from either place, is emailed to `OWNER_EMAIL` ("New feedback (bug)", the sender in the body, not the subject, which failed sends log) with the sender as Reply-To, so the owner answers by replying; none for the owner's own comments.

## 7. Notifications

**Email layout** (issue #79): every email the app sends (account, notifications, feedback to the owner, the admin test email) uses one layout in the app's cyberpunk style, built by `EmailDocument`: a "▲ STRATEGY TRADING LAB" brand line, a card on the dark ground with a mint top edge, a small uppercase label for the kind of email ("Signal · Live", "Daily overview", "Your account"), then the content, the short disclaimer at the foot of the card wherever signals or results appear, and a footer with "Open the app". The main link is a button with the address under it ("Or open this link:"), for clients that block buttons; the overview's sections are lists with *Live* (pink) and *Simulation* (cyan) tags, and each waiting item links to its page. Each email is sent as HTML and as plain text (multipart/alternative) built from the same blocks, so both say the same thing; clients that don't show HTML show the text. Tables and inline styles only (mail clients drop `<style>`), the text 16px, buttons at least 44px tall, at most 560px wide. Dark only, like the app: assumed, since mail apps that force a light theme may recolour it. The footer doesn't say "you get this because you have an account": a sign-up confirmation can reach someone who never signed up.

**Daily overview** (email, not drawn), once per US session, right after the daily check has evaluated it (around 22:30 in Madrid), titled with the session's date; whoever wasn't sent it then (email not set up yet, a restart, an address just confirmed) gets it from the hourly check, but never between 00:00 and 07:00 in their time zone: it waits for the morning. A time of the user's choice comes with the overview settings (after launch). Reads like a briefing; never uses buy or sell as an instruction.
1. **Waiting for you**: unanswered live signals and AI rounds due, each with a link.
2. **What changed today**: e.g. "ZS fell 8% from its recent peak and is still above its 50-day average (average price over 50 days). That meets the rules of your strategy *Zscaler & friends*." For simulations, also today's signal trades.
3. **Close to the line**: the stocks nearest to each strategy's rules, with the distance.
4. **The scoreboard**: one line per strategy, with its name, mode and result so far.
5. Every item links to its page. The disclaimer at the bottom.

**Live signal alert**: web push and email, right after the daily check that found it (or after an AI round). Same text as the signal box. Sent once per channel; a snoozed signal is notified again the next day, once per channel.

**AI round due**: notification and email: "Time for the AI round of [strategy name]. It takes about two minutes." (built: email only, once per user and due day, from 07:00 in the user's time zone, listing every round of theirs due that day, with a link to each; not for a strategy's first round, due the day it starts; only for strategies with notifications on and a confirmed email). The daily overview's *Waiting for you* lists AI rounds due too. A live signal from a round still waiting after the next daily check gets the usual alert then.

## 8. Data the app needs

The schema is in [backend/DATA-MODEL.md](../backend/DATA-MODEL.md); this is the summary.

- **User:** email, password hash, verified flag, failed log-in count and lock time, sessions and email links (verify, reset), time zone, language (English or Catalan), overview email on/off, disclaimer accepted at, push subscriptions, AI data sharing on/off and when changed.
- **Template:** static, in the code, not the database: key, copy (per language), tested defaults with a version number, starter list or fund.
- **Asset:** a stock or fund: symbol, exchange, name, what it does, currency; for added ones, sector and industry from the price source, who added it and when, and whether it is hidden. Seeded or added, it is a plain row: adding one needs no migration or deploy. Prices are fetched daily for the assets some running strategy lists, and for the index fund the results compare with.
- **Invites:** the link's code, who made it, uses left; each account records who invited it.
- **Strategy:** user, template key, the tested-defaults version it started from, name, mode, start date, status (running or ended; no pause), when it ended and when it was archived (ended ones only), notifications on/off, currency, budget (budget, step size, max steps per stock; editable), parameter values, and for a live copy the simulation it came from. AI settings (round frequency, top N, sources, the user's instructions) are in the parameters.
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

- The rest of the account page: time zone, and the rest of Data and privacy (export, delete account). Language and overview email are designed above; how they work is in [backend/AUTH.md](../backend/AUTH.md). Built: "Data and privacy" with "Share my AI rounds anonymously, to learn which AIs score stocks best." and "What we keep".
- Simulation variants of Set your budget, Confirm and the strategy page. (The AI round's Live variant is built: it opens Confirm a signal for the first pick.)
- Pick screen when adding a strategy (back link, "You run [n] from this template").
- Budget variants for *Index trend* and *Bear markets*.
- Stock page: chart with the discount zone shaded, status history, trades (live: confirmed, skipped and manual, each marked; rules' position vs the user's).
- Stock list editor from the strategy page ("Edit list"; "+ Add a stock" on the review is built, #90).
- Until a screen is built, the controls that open it are hidden, not shown disabled: "Edit list", the "Backtest" tile, and Confirm's "Edit" on the Daily overview and Alerts rows. The item that builds the screen brings its control back.
- Sell signals for *Index trend*, and its "Out of the market" status.
- ~~Manual trade form; editing and deleting trades.~~ (built in Signals and trades section, #61)
- The AI comparison page (later: results by AI model, never a ranking of stocks). The round history and the error state are built.
- Backtest page; History (every signal, trade, skip, AI round and status change, in date order).
- Changing the budget.
- Snooze on Confirm a signal; "Enter shares instead" and the exchange-rate line.
- Daily overview email and its settings.
- Empty, loading and error states.

Needed to publish, and not in this spec yet (proposed in [ROADMAP.md](../ROADMAP.md) → *The launch product*; each gets its section here when it is built):

- "Coming soon" cards on Pick a template for templates the engine can't run yet.
- Live mode gated on a confirmed email, with email-only alerts until web push exists.
- ~~Statuses for *Bear markets*; the "Signals and trades" list on the strategy page.~~ (both built: #60, #61)
- ~~Ended strategies.~~ (built #91: kept read-only with an "Ended" mark; the user can archive them)
- The legal pages (legal notice, terms, privacy, disclaimer, About with conflicts of interest), linked from a footer.
- Account: download my data, delete my account, "Invite a friend".
- Unsubscribe links in emails.
- Admin: health and failure alerts, users (block, delete), invites and test invites, adding and editing assets, an audit log of owner actions, and feedback.
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
