# Strategy Trading Lab: app spec for implementation

Oct 15, 2026 · @Ernest

Is the productized version of https://github.com/ernestbofill/trading-strategies-bot

This is the brief for the agent building the Strategy Trading Lab app. It describes what the app is, every screen, how the screens connect, what each one does, and the exact copy. Build from this file.

- **Wireframes:** [Strategy Trading Lab wireframes · Cyberpunk](../wireframes/). Their source files are one `.dc.html` file per screen (named in the table in section 4) plus `canvas.json`, which lists the screens and the designer's notes. They are in the design tool's own format and don't run on their own, but their markup shows each screen's layout, copy and interactions.
- **Visual style:** use the [Cyberpunk design system](../design-system/cyberpunk/).
- **Product background:** `claude/stab-product-user-journey.md`. **Regulation:** `claude/regulation-spain-eu.md`.
- **Strategy logic:** already exists in the POC (`ernestbofill/trading-strategies-bot`, see `docs/PLAN.md`).

**How to read this spec.**
- Text in square brackets, such as `[+X.X%]` or `[Stock name]`, is a placeholder for live data or for content still to be written.
- Everything else in quotes is final copy, to be used exactly as written. Section headings in the wireframes are written in normal case and shown in capitals by the style.
- Parts marked **(not drawn)** come from the user journey and have no wireframe yet. Their copy is a draft: use it, but expect it to change once the screen is designed.

## 1. What the app is

Strategy Trading Lab is a strategy lab and a monitoring side app. A user picks a **strategy template**, sees how it did in the past, tunes its rules, gives it a budget and a name, and runs it as their own **strategy** in one of two **modes**:

- **Simulation:** the app trades on paper, on real prices, and shows how the strategy does from today. Nothing happens outside the app.
- **Live:** the user follows the strategy with real money at their own broker. The app sends each signal, the user says whether they acted on it or skipped it, and the app records those trades and tracks the results.

There is no broker sync in either mode. The app never places orders and never reads the user's broker account; in live mode it knows only what the user confirms. It gives no trading advice.

## 2. Words

| Word | Means |
| --- | --- |
| **Strategy template** | An idea and its rules, as offered in the catalogue, with our tested defaults. Picked, reviewed, backtested and tuned. Templates never change because of what a user does. |
| **Strategy** | The user's own copy of a template: their name for it, their stocks, settings and budget, running in one mode from the day they start it. It keeps its own trades and results. A user can run any number of strategies, several from the same template. |
| **Mode** | *Simulation* or *Live*. Fixed for the life of a strategy; going live means starting a new strategy. |
| **Signal** | The moment a strategy's rules are met for a stock: a buy step, or, for *Ride the long trend*, a sale. In simulation the app acts on it by itself; in live it asks the user. |
| **Step** | One buy of the step size. A stock gets at most the max steps per stock. |
| **Discount zone** | For *Dip in winners*: the range 5-15% below a stock's recent peak, where its rules signal. |
| **AI round** | For strategies from *Your AI's top picks*: every 2 weeks the user runs our prompt in their own AI service and pastes the answer back. |
| **Backtest** | How a template did in the past with given settings. It belongs to the template and settings, not to a running strategy. |

The words *ticker* and *tranche* never appear in the app: use *stock* (or *fund*) and *step*.

## 3. Product rules

These apply to every screen, email and alert.

- **Describe, don't instruct.** Template names describe the situation the rules look for. On a template's page the strategy is the subject ("It signals when…"), never the user. Never "buy now", no urgency, no price targets or return forecasts.
- **Signals report rules.** Every signal says which rule was met, the facts that met it and what that means under the user's settings.
- **The user owns the setup.** Lists, settings and budgets are the user's choices, starting from our tested defaults: "Choose stocks to watch", "Under your settings", never "recommended for you".
- **No promises.** No claims that markets or stocks will rise or recover. Results are always shown next to the index fund and the worst year.
- **One disclaimer, everywhere results or signals appear:** "Not financial advice. Signals follow your rules; you decide and trade at your own broker."
- **No broker sync.** No broker links, no prefilled orders, no reading of the user's account.
- **Plain language.** Real trading terms stay, each explained in brackets the first time it appears, e.g. "SMA 200 (average price over the last 200 days)".
- **US markets, euro results.** v1 covers US-listed stocks and funds. Budgets, steps and results are in euro (`€3,000`), converted at the day's exchange rate; stock prices are shown in dollars (`$412.30`), as at the user's broker.
- **Mode always labelled.** Every strategy shows *Simulation* or *Live*; results of different modes are never mixed.
- **Free for now.** No paywall, plans or pricing.
- **Mobile first.** The wireframes are 390 px wide. Every screen must also work on desktop, with no horizontal scrolling at any width.
- **Functional style rules.** One primary action per screen, at the bottom. Selected items look clearly different from unselected ones. Touch targets at least 44 × 44 px, including − / + steppers. Status is never shown by colour alone: every status has a text label. Text meets WCAG AA contrast.

## 4. Screens and navigation

| Screen | Wireframe file | Reached from | Goes to |
| --- | --- | --- | --- |
| Sign-up (not drawn) | – | App start | Pick a template |
| 1 · Pick a strategy template | `Main` | Sign-up; "+ New strategy" on the dashboard | The picked template's review |
| 2 · Review a template (one per template) | `Review`, `ReviewComeback`, `ReviewDrop`, `ReviewTrend`, `ReviewAI` | Pick screen "Continue" or "Learn more"; Edit links on Confirm | Advanced settings, How we tested, Choose the mode |
| 2b · Advanced settings | `Advanced` | Review; "Tune the settings" on the strategy page; Confirm | Back to where it came from |
| 2c · How we tested | `HowTested` | "How we tested ›" on a review (not on the AI review) | Back to that review |
| 3 · Choose the mode | `Mode` | Review "Use this template"; "Go live with these settings" (not drawn) | Set your budget |
| 4 · Set your budget | `Budget` | Choose the mode; Confirm | Confirm |
| 5 · Name, preview and confirm | `Confirm` | Set your budget | Dashboard, or the first AI round |
| Dashboard (home) | `Home` | Confirm; app start for returning users | Strategy page, Confirm a signal, AI round, Pick a template |
| Strategy page | `StrategyDetail` | A strategy card on the dashboard | Stock page (not drawn), stock list editor, Advanced settings, Backtest (not drawn), Confirm a signal |
| Confirm a signal (live) | `Signal` | Waiting for you on the dashboard; a stock row; a signal alert or the daily overview | Strategy page |
| AI round | `AIRound` | Waiting for you on the dashboard; the AI-round notification; Confirm for a new AI strategy | Dashboard (simulation); Confirm a signal (live) |

**New user:** sign-up → pick → review → (advanced settings, how we tested) → mode → budget → confirm → dashboard (or the first AI round). A new user never sees an empty dashboard.

**Adding a strategy:** dashboard "+ New strategy" → the same screens. All templates are shown, with "You run [n] from this template" on templates already in use (not drawn).

**Going live from a simulation (not drawn):** "Go live with these settings" on a simulation's page starts the flow at Choose the mode, with Live selected and the stocks, settings and budget copied. The simulation keeps running or can be ended; the two keep separate histories.

**Headers.** Every screen of the new-strategy flow has a top bar with "← BACK" on the left and the step ("1 / 5" to "5 / 5") on the right. The first screen shows "STL // NEW STRATEGY" instead of a back link (when reached from the dashboard, show "← BACK" to the dashboard). Back goes to the previous screen.

## 5. The templates

Five templates. The first four exist in the POC; *Your AI's top picks* is new. Use the POC defaults below as the tested defaults. Where the screen copy simplifies a rule, the POC rule is what the engine runs.

| Template | POC key | Tagline | Invests in | Signals |
| --- | --- | --- | --- | --- |
| Dip in winners | `stage2_pullback` (Leader pullback) | "Find the next Nvidia" | A list of stocks | Buys only |
| The comeback | `turnaround` (Confirmed turnaround) | "Stocks on the rise" | A list of stocks | Buys only |
| The big market drop | Bear-market accumulation | "When the market is on sale" | One S&P 500 fund (an ETF) | Buys only |
| Ride the long trend | `index_trend` (Index trend) | "Take the wins, dodge the losses" | One broad index fund | Buys **and sells** |
| Your AI's top picks | new | "Let your AI do the reading" | A list of stocks | Buys only |

### Engine defaults per template

**Dip in winners (Leader pullback)**
- Candidate (trending): gained at least 50% at some point in the last 4–8 months; price > SMA 50 > SMA 150 > SMA 200; the SMA 200 is rising.
- Signal: price in the discount zone, 5–15% below the recent peak (highest price of the last 20–60 days; the screen exposes one number, default 40), while still above the SMA 50.
- Signal ends: the price leaves the zone (back up to the old high, or below the SMA 50).
- Later steps: one per signal window, at least 20 days (cooldown) after the last one. The cooldown is not exposed in Advanced settings.

**The comeback (Confirmed turnaround)**
- Candidate (beaten down): at least 30% below its 52-week high, and below the SMA 200.
- Signal: the close is at least 15% above the SMA 50, and 5-day average volume is at least 20% above the 50-day average.
- Signal ends: the close drops back under the signal line, or the price is more than 5% above it.
- Later steps: one per milestone (back above the SMA 200, golden cross, within 15% of the 52-week high, breakout after an 8% pullback), each within 5% above its level. Cooldown 5 days.

**The big market drop (Bear-market accumulation)**
- Normal → Drawdown (−20% from the high of the last 6 months) → track the new low → Signal (+5% above the lowest low; first step) → Accumulating (one step every 2 weeks, 5 steps by default) → Done.
- Resets on a new high, or once the cycle's buys are done and the drop was first seen at least 126 trading days ago; then a new 20% fall starts a new cycle.

**Ride the long trend (Index trend)**
- Buy signal: the close is more than 3% above the SMA 200. Sell signal: the close is more than 3% below it. In between, the trend stays what it was.
- All in, all out: one buy of the whole budget; after a sale, the next buy reinvests what the sale returned. Cooldown 5 days. One buy signal per uptrend.
- No stop-loss: the sell line is the stop.

**Your AI's top picks (new)**
- Every 2 weeks (default), the user runs the AI round (section 6, AI round). The app runs no AI itself.
- After each round, the top-scored stock gets a signal for one step (default top 1), within max steps per stock and the budget.
- If the user doesn't run a round within a week of its due date, it is marked *Skipped*: no signal. The next round comes 2 weeks after the due date.
- No backtest: results start on the day the strategy starts.

**Exits.** Only *Ride the long trend* sells. The POC's stop-loss and trailing stop for Leader pullback and Confirmed turnaround are not part of the product. In buy-only templates, a user who sells at their broker logs the sale by hand (live).

### How the two modes run

- All rules are checked **once a day, after the US market close**, as in the POC.
- **Signals follow the rules, not the user's trades.** Each strategy keeps the rules' own paper position, and signals come from it, in both modes. After a skip, the rules carry on as if the step had been bought (the next signal for that stock is the next step), and a *Ride the long trend* sell signal arrives even if the user never bought.
- **Simulation:** the app records every trade the rules call for, at the next trading day's opening price. Results come from those paper trades.
- **Live:** the app sends each signal and the user confirms it. Results come only from confirmed trades at the prices the user entered; the rules' paper position is shown next to them.
- **Budget:** when it is used up, the strategy stops signalling buys. In live mode, only confirmed trades use the budget.

## 6. Screen by screen

### Sign-up (not drawn)

- Email and password, with email verification.
- Daily overview by email, on by default.
- Time zone, guessed from the browser and editable. It sets when the overview arrives (early morning in Europe).
- The disclaimer, accepted once: "Not financial advice. Signals follow your rules; you decide and trade at your own broker." Plus one line: the app is for learning, testing and following strategies, and it never trades for you.
- No push permission and no AI data sharing at sign-up: those are asked when first needed.
- On success, go straight to Pick a strategy template.

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
| Dip in winners | "Find the next Nvidia" | "A stock on a strong run takes a small step back. The strategy signals that dip." |
| The comeback | "Stocks on the rise" | "A stock that fell a lot starts to recover. The strategy looks for a confirmed turnaround." |
| The big market drop | "When the market is on sale" | "When the whole market falls 20%, the strategy signals buys of a broad S&P 500 fund in small steps as it recovers." |
| Ride the long trend | "Take the wins, dodge the losses" | "The strategy signals a buy of a broad index fund when the market moves into an uptrend, and a sale when the trend turns down." |
| Your AI's top picks | "Let your AI do the reading" | "Every 2 weeks the AI scores each stock from 0 to 100, and the top score should get a position. Use your own AI." |

**Behaviour**
- Single choice. Tapping a card selects it; the selected card is highlighted and shows a filled check, the others an empty box.
- *Dip in winners* is selected by default, so "Continue" is always enabled.
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
- Last row: "+ Add a stock" (search US-listed stocks by name; not drawn).
- Under the list: "ⓘ In our tests, longer lists gave steadier results."
- The starter lists come from the POC. Zscaler ("Cloud security software") is the only real example in the wireframes.

**Fund block** (fund templates)
- Heading "The fund it watches". One row: the fund's name, a one-line description, and a "Change" link (fund picker, not drawn).

**Per-template content**

*Dip in winners* (`Review`)
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

*The big market drop* (`ReviewDrop`)
- The idea: "Big market crashes are rare and scary, and most people sell. Instead of guessing the bottom, the strategy waits for the first bounce and then signals buys in small steps while prices are still low."
- It signals when:
  1. "**The market fell hard:** the S&P 500 is 20% or more below its high."
  2. "**The first bounce:** it climbs 5% off its low. That's the first step."
  3. "**Then in steps:** one more step every 2 weeks, until the budget is used."
- Fund: "[S&P 500 fund name]" / "Tracks the 500 largest US companies" / "Change".
- Tested: "[N] big drops, [year]-[year]."
- What can go wrong: "the market can keep falling after the first bounce, and stay low for years."

*Ride the long trend* (`ReviewTrend`)
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
  1. "Every 2 weeks you run the AI round: copy the prompt, paste it into your AI, paste its answer back."
  2. "The stock with the top score gets a signal for one step, until the budget is used."
  3. "If you skip a round, there's no signal that round."
- **"What the AI reads"**: three toggle tiles, all on by default: "News" / "Recent stories", "Mood" / "How people talk about it", "Figures" / "Quarterly numbers". On shows "✓" and a highlight, off shows "+". At least one stays on: the last one on can't be turned off. Use `aria-pressed`. Caption: "At least one stays on. Each one adds a section to the prompt."
- **"The prompt"**, with an "Edit prompt" link (editor with "Reset to suggested", not drawn). Read-only by default, shown as code:
  - "// suggested prompt · v1"
  - "Today is [date]. Search the web for news and data from the last 2 weeks, plus each company's latest quarterly figures."
  - "Score each stock below from 0 to 100 on:"
  - one line per source switched on: "· News: [criteria]", "· Mood: [criteria]", "· Figures: [criteria]"
  - "and overall. Give a one-line reason and your main sources, with dates."
  - "Answer only in this table: | Stock | [sources on] | Overall | Reason |"
  - "Stocks: [your stocks to watch]"
  - Caption: "It asks for scores against stated criteria, never which stock to buy."
  - The prompt never includes anything about the user (no budget, goals or holdings). The date window follows the round frequency.
- Info box: "**What you need:** an AI service that can search the web (most paid plans can). The app doesn't connect to it and never sees your account."
- "**Not tested:** there's no backtest. Results start on the day the strategy starts, and depend on which AI you run it in."
- "**What can go wrong:** your AI can misread the news, or make things up, and a top-scored stock can still fall."
- Rows: "Choose stocks to watch · [n] ticked ›" (opens the stock list) and "Advanced settings · every 2 weeks · top 1 ›".

### 2b · Advanced settings (`Advanced`)

**Purpose:** tune each rule as an exact number and see the effect on the backtest. Drawn for *Dip in winners*; every template needs its own set, from its POC parameters (open question). For *Your AI's top picks* it holds the round frequency (default every 2 weeks) and how many top-scored stocks get a step per round (default 1), with no backtest.

**Copy**
- Top bar: "← BACK" · "[template name]"
- H1: "Advanced settings"
- Intro: "Every rule as an exact number. Change one and the backtest re-runs, next to the tested defaults."
- Secondary button: "⟳ Reset to tested"
- Button: "Save settings"

**Setting rows**: the setting as a sentence with the value highlighted, a − / + stepper on the right, and "Tested: [default]" under it. A changed row is outlined and shows a "CHANGED" label.

| Sentence | Default | Step | Min–max |
| --- | --- | --- | --- |
| "The discount zone starts [n]% below the recent peak." | 5% | 1 | 1 – (end − 1) |
| "It ends [n]% below the recent peak." | 15% | 1 | (start + 1) – 40 |
| "The recent peak is the highest price of the last [n] days." | 40 days | 5 | 20–60 |
| "The long trend is the average price over [n] days." | 200 days | 10 | 100–300 |
| "The dip must stay above the average over [n] days." | 50 days | 5 | 20–100 |

**"Backtest"** section
- On the defaults: "You're on the tested defaults. Change a setting to compare."
- As soon as any value differs: a table "Period" / "Tested" / "Yours" with rows "[2010-2014]", "[2015-2019]", "[2020-2024]", "All years", and the caption "Several periods, so one lucky stretch doesn't decide. Past results don't promise future ones."
- Re-run on each change (debounced), with a loading state.

**Behaviour**
- "Reset to tested" puts every value back.
- "Save settings" stores the values and returns to the screen that opened it. Leaving with unsaved changes asks first (not drawn).
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

Content is per template (period, scope, figures). *Ride the long trend* is all in, all out, so its "tested defaults" line reads "The whole €3,000 budget, all in, all out." (draft).

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
| The app: | "records each trade at the next day's opening price." | "alerts you to each signal and records what you confirm." |
| You: | "nothing. Read the daily overview." | "confirm each signal: I did it, or I skipped it." |

**When Live is selected**
- An info box "Alerts": "Your browser will ask to allow notifications, so signals reach you right after the market close. Email alerts are on too, as a fallback. On iPhone, add the app to your home screen first."
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
- Under the button: "Amounts in euro. Dollar prices convert at the day's rate."

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
- *Ride the long trend*: budget only (all in, all out).
- *The big market drop*: one fund, so "Max steps per stock" is the number of steps for the fund (default 5).
- *Your AI's top picks*: add "One step every 2 weeks uses the budget in about [n] weeks."

### 5 · Name, preview and confirm (`Confirm`, drawn in its Live variant)

**Copy**
- Top bar: "← BACK" · "5 / 5"
- H1: "Ready to start"
- **Name field**, label "Name your strategy". Prefilled with the template's name, numbered if the user already has one by that name ("Dip in winners 2"). The wireframe shows a user-typed example, "Zscaler & friends".
- Under the field, live preview: "[name] · [template] · [Live|Simulation] · from today"
- **Summary**, one row per item with an "Edit" link:

| Label | Value | Edit → |
| --- | --- | --- |
| Template | "[template name]" | Pick a template |
| Mode | Live: "Live: you trade at your broker and confirm each signal" · Simulation: "Simulation: the app trades on paper" (draft) | Choose the mode |
| Stocks | "[first stock] and [n] more" (fund templates: the fund's name) | Review |
| Settings | "Tested defaults", or "[n] changed" | Advanced settings |
| Budget | "€[budget] · steps of €[step] · up to [max] per stock" | Set your budget |
| Daily overview | "By email, after the US market close" | Overview settings (not drawn) |
| Alerts (Live only) | "App and email" | Choose the mode |
| Prompt (AI only, not drawn) | "Suggested" or "Edited" | AI review |

- **"What happens next"**
  - Live: "You see right away how the template did in the past." / "From today the app watches your stocks and alerts you as soon as your rules are met." / "You confirm each signal, and the app tracks the results of what you did."
  - Simulation (not drawn): "You see right away how the template did in the past." / "From today it runs on real prices and records its own paper trades." / "Your first daily overview arrives tomorrow."
  - *Your AI's top picks* (not drawn): "There are no past results for this template." / "Your first AI round is due now." / then the mode's line.
- Above the button: the disclaimer.
- Button, Live: "Start following it live". Simulation (not drawn): "Start the simulation".
- The button creates the strategy and opens the dashboard; for *Your AI's top picks*, its first AI round.

### Dashboard (`Home`)

**Copy**
- Top bar: "STL // DASHBOARD" · "ACCOUNT" (link to the account page, not drawn)
- H1: "Your strategies"
- Bottom: the disclaimer.

**"Waiting for you"** (shown only when something is waiting), with a count badge. One row per item, newest first, each a link:
- Live signal, tag "LIVE": "Your rules for **[strategy name]** are met for [stock]. Did you act on it?" → Confirm a signal.
- Live sell signal (not drawn): "Your rules for **[strategy name]** are met for a sale. Did you act on it?"
- AI round due, tag "AI": "Time for the AI round of **[strategy name]**." → AI round.

**Strategy cards**, one per strategy; the whole card links to its strategy page.
- Header: the strategy name; under it "*[template]* · since [date]" or "*[template]* · started today"; on the right the mode tag ("LIVE" or "SIM"). A small glowing dot before the tag marks a card with something waiting, with an accessible label ("Signal waiting" or "AI round due"). The Waiting for you list carries the detail; the dot only points to the card.
- Running: the result since start, big, "[+X.X%]"; under it "Index fund: [+X.X%]"; a small line chart on the right; a budget bar and "€[used] of €[budget] used".
- Started today: "No paper trades yet. In the past, [year]-[year], the template did [+X%] vs [+X%] for an index fund." (Live: "No trades yet. …", draft.) An empty budget bar, "€0 of €[budget] used".
- AI strategy with no results: "No results yet. They start after your first AI round."
- Below the cards: a dashed button "+ New strategy" → Pick a template.

### Strategy page (`StrategyDetail`, drawn in its Live variant)

**Copy and layout**
- Top bar: "← STRATEGIES" · "RENAME" (renames the strategy in place; not drawn)
- H1: the strategy name; under it the mode tag and "*[template]* · since [date]".
- **Summary box**
  - Live: three figures, "You" `[+X%]`, "The rules alone" `[+X%]`, "Index fund" `[+X%]`; a chart with three lines (You solid and highlighted, The rules alone solid, Index fund dashed) and a legend; then "Budget used" / "€[used] of €[budget] · [n] of [total] steps" and "Signals" / "[n] did · [n] skipped · [n] missed".
  - Simulation (not drawn): "Since start" `[+X%]` and "Index fund" `[+X%]`, a two-line chart, and "Budget used".
- **"Stocks · [n]"**, with an **"Edit list"** link on the right that opens the stock list editor (same list as on the review, for this strategy).
- One row per stock, each a link to that stock's page (not drawn):
  - The name, a status pill and a detail line:
    - "WAITING FOR YOU" (live; "Signal: step [n] of [max]") → Confirm a signal
    - "SIGNAL" (simulation; "Bought a step today", draft)
    - "HOLDING" ("[n] of [max] steps")
    - "BUILDING" ("[n] of [max] steps"; live when it differs: "Rules: [n] of [max] · You: [n] of [max]")
    - "WATCHING" ("[n]% from the discount zone", or "Below its long trend" when it isn't a candidate)
    - *Ride the long trend* also needs "OUT OF THE MARKET" for the time between a sale and the next buy (not drawn).
  - On the right: the stock's own result "[+X.X%]" with "You €[invested] in" (simulation: "€[invested] in"), or "–" with "No buys yet".
- Under the list: "Removing a stock stops its signals. Your trades in it stay in the results."
- Two buttons: "Tune the settings" / "Change its rules" → Advanced settings; "Backtest" / "How the template did" → Backtest page (not drawn). For AI strategies the second is "AI rounds" (round history, not drawn).
- For simulations, a third link: "Go live with these settings" (not drawn).
- Pause and end the strategy (not drawn).
- Bottom, Live: "Your results come from the trades you confirmed, at your prices. Not financial advice. Signals follow your rules; you decide and trade at your own broker." Simulation (draft): "Results come from paper trades at the next day's opening price. Not financial advice. Signals follow your rules; you decide and trade at your own broker."

**Metrics**

| Metric | Simulation: from | Live: from |
| --- | --- | --- |
| Budget used | Paper buys vs budget | Confirmed buys vs budget |
| Average cost | Paper buys | Confirmed buys, with fees |
| Current value | Paper buys, latest price and exchange rate | Confirmed trades, latest price and exchange rate |
| Gain or loss (money and %) | Current value − invested | Same |
| Versus the index | Same money into a broad index on the same days | Same, on the confirmed buy days |
| Versus the rules | – | The rules' paper position |
| Followed signals | – | Did / skipped / missed |
| Picks vs the rest (AI only) | Top-scored stocks vs the unpicked ones since each round | Same |
| Next up | Stocks closest to their rules, with the distance | Same |

### Confirm a signal (`Signal`, live only)

**Copy**
- Top bar: "← BACK" · "SIGNAL · [DATE]"
- Above the H1: the "LIVE" tag and "[strategy name] · *[template]*"
- H1: the stock's name
- Signal box: "Your rules for **[strategy name]** are met for [stock]: [8]% below its recent peak, still above its 50-day average. Under your settings that's step [2] of [5] ([€100])." Then "Close on [date]: $[XXX.XX]". The fact list depends on the template's rules.
- Sell signal for *Ride the long trend* (not drawn): "Your rules for **[strategy name]** are met for a sale: the market closed [3.2]% below its 200-day average."
- **"Did you act on it?"** with two choices side by side: "✓ I did it" and "× I skipped it". "I did it" is preselected in the wireframe.

**I did it**: four fields, each with a visible label:

| Label | Prefill |
| --- | --- |
| Date | The next trading day |
| Price per share | The next trading day's opening price, once the market has opened. Before that, the field is empty and the signal day's close is shown as a reference. |
| Amount | The step size |
| Fee (optional) | Empty, placeholder "€0.00" |

Help line: "Price is prefilled with [date]'s opening price. Change anything that differs from your broker." Button: "Save the trade".

**I skipped it**: "Why? (optional)" with single-choice chips "Price ran away", "Didn't trust it", "No cash", "Other". Text: "A skip uses no budget. The rules carry on as if the step was bought, so the next signal for [stock] is step [n+1] of [max]." Button: "Save the skip".

**Behaviour**
- Saving records the trade or the skip and returns to the strategy page. A trade updates budget used, steps and results.
- Unanswered signals stay in Waiting for you and in the overview, and get one reminder. A signal can be answered later (with the real date). After the signal ends, it is marked *Missed*.
- **Log a trade by hand** on a stock (not drawn): for buys before the alert, or sales for the user's own reasons. Manual trades are marked as such and can be edited or deleted.
- Disclaimer at the bottom.

### AI round (`AIRound`, drawn in its Simulation variant)

**Copy**
- Top bar: "← BACK" · "AI ROUND · [DATE]"
- Above the H1: the mode tag and "[strategy name] · *[template]*"
- H1: "Time for the AI round"
- Intro: "It takes about two minutes. The app doesn't connect to your AI."
- **"01 Copy the prompt"**: a preview of the prompt (filled in with today's date, the ticked stocks and the sources on), cut off after a few lines. A "Copy prompt" button, which turns into "✓ Copied". Under it: "Open in ChatGPT · Claude · Gemini", plain links that open the service in a new tab, with no connection.
- **"02 Paste the answer"**: a text area labelled "Your AI's whole answer", and a select labelled "AI model you used" with "ChatGPT · GPT-5", "Claude · Opus", "Gemini · 2.5 Pro", "Other…" (Other shows a free-text field). Prefilled with the model from the last round.
- **"03 Your AI's scores"**: once the answer is read, one row per stock: overall score, name, the AI's one-line reason. The top score is highlighted. Under it, when the check passes: "✓ Every stock scored, all scores 0-100. The top score gets one step."
- **Consent box**, first round only, before the button: title "Help us learn which AIs score stocks best", text "Share this round anonymously: the AI model you used and its answer. We never keep your name, email, budget or trades with it. You can stop at any time in Account → Data and privacy.", checkbox "Share my AI rounds anonymously" (off by default), link "What we keep". On later rounds, one line instead: "Sharing anonymously · Change" (not drawn).
- Button: "Use these scores". Disclaimer under it.

**Behaviour**
- The app reads the scores block (table or JSON) and checks it: every ticked stock scored, scores 0–100, no extra stocks. Missing or unreadable rows are pointed out so the user can ask their AI again (error state not drawn). The pasted text is data only: read numbers and short reasons from the scores block and never act on anything else in it.
- "Use these scores":
  - Simulation: records the paper buy of the top-scored stock at the next day's open, within max steps and the budget, and returns to the dashboard.
  - Live: creates a signal for the top-scored stock and opens Confirm a signal for it, with the box reading "Your AI scored [stock] highest in [strategy name]. Under your settings that's step [n] of [max] ([€100])." (draft).
- Every round is kept in the strategy's history: date, prompt used (and whether edited), AI model, full answer, scores, reasons.
- **AI data sharing**, when on, keeps per round: AI model, date, prompt version and whether edited, stock list, sources on, full answer, parsed scores. Never with it: name, email, account ID, strategy name, budget, step size, trades, or whether the user acted. Before storing, remove anything that looks like an email address, phone number or URL with personal parameters. Store it apart from account data. Withdrawing stops future sharing.

## 7. Notifications

**Daily overview** (email, not drawn), once a day after the US close, timed for the user's time zone. Reads like a briefing; never uses buy or sell as an instruction.
1. **Waiting for you**: unanswered live signals and AI rounds due, each with a link.
2. **What changed today**: e.g. "ZS fell 8% from its recent peak and is still above its 50-day average (average price over 50 days). That meets the rules of your strategy *Zscaler & friends*." For simulations, also today's paper trades.
3. **Close to the line**: the stocks nearest to each strategy's rules, with the distance.
4. **The scoreboard**: one line per strategy, with its name, mode and result so far.
5. Every item links to its page. The disclaimer at the bottom.

**Live signal alert**: web push and email, right after the daily check that found it (or after an AI round). Same text as the signal box. One reminder if unanswered.

**AI round due**: notification and email: "Time for the AI round of [strategy name]. It takes about two minutes."

## 8. Data the app needs

- **User:** email, password hash, verified flag, time zone, overview email on/off, disclaimer accepted at, push subscriptions, AI data sharing on/off and when changed.
- **Template:** static, from the code: key, name, tagline, copy, tested defaults, starter list or fund.
- **Strategy:** user, template key, name, mode, start date, status (running, paused, ended), stock or fund list with ticked state, parameter values, budget (budget, step size, max steps per stock), AI sources on/off and prompt (suggested or edited text, version), and for a live copy the simulation it came from.
- **Rules position:** per strategy and stock, the paper trades the rules made (date, side, price, amount, step). Drives signals in both modes and the results in simulation.
- **Signals:** strategy, stock, side, step, the rule facts, date, status (waiting, did, skipped, missed), skip reason, reminder sent at.
- **User trades (live):** strategy, stock, date, side, price, amount or shares, fee, source (signal or manual), linked signal.
- **AI rounds:** strategy, due date, status (due, done, skipped), date run, prompt text and version, edited flag, AI model, full answer, parsed scores and reasons.
- **Shared AI rounds:** a separate store with no account ID (see the AI round). Whether it is truly anonymous or pseudonymous is open (section 10).
- **Daily evaluation:** per strategy and stock: status, distance to the rules, signal start and end.
- **Backtest results:** per template, parameter set and period: result vs the index, worst year, number of trades. Cached by parameter set.
- **Market data:** daily US prices and volumes, the euro-dollar rate.

## 9. Not designed yet

- Sign-up, log-in and the account page (including Data and privacy: AI sharing on/off, What we keep, export, delete account).
- Simulation variants of Set your budget, Confirm and the strategy page; the Live variant of the AI round result.
- Pick screen when adding a strategy (back link, "You run [n] from this template").
- Budget variants for *Ride the long trend* and *The big market drop*.
- Advanced settings for the other four templates.
- Stock page: chart with the discount zone shaded, status history, trades (live: confirmed, skipped and manual, each marked; rules' position vs the user's).
- Stock list editor from the strategy page, stock search ("+ Add a stock"), fund picker ("Change").
- Prompt editor ("Edit prompt", "Reset to suggested").
- Sell signals for *Ride the long trend*, and its "Out of the market" status.
- Manual trade form; editing and deleting trades.
- AI round history, error state when the answer can't be read, and the AI comparison page (later: results by AI model, never a ranking of stocks).
- Backtest page; History (every signal, trade, skip, AI round and status change, in date order).
- Go live with these settings; renaming; pausing or ending a strategy.
- Daily overview email and its settings.
- Empty, loading and error states.

## 10. Open questions

- **Regulation.** See `claude/regulation-spain-eu.md`. For the legal review: the taglines "Find the next Nvidia" (brand name, hints at exceptional returns) and "Take the wins, dodge the losses" (could read as a promise to avoid losses), the suggested AI prompt, the default stock lists, and the AI comparison page.
- **Anonymous or pseudonymous AI data.** Decide before building, and word the consent box to match.
- **Edited prompts** in the AI comparison: keep, keep apart, or leave out?
- **The suggested prompt:** who writes and versions it, and how it's tested across ChatGPT, Claude and Gemini.
- **Reading the answer:** how forgiving the parser is (table vs JSON, extra text, names instead of symbols).
- **AI model list:** which models are in the picker, and who keeps it current.
- **Advanced settings per template:** which POC parameters each template exposes; whether *Dip in winners* should expose its 20-day cooldown.
- **Price data:** which provider supplies daily US prices and the euro-dollar rate, with terms that allow commercial use, and its cost at many free users. The POC uses eToro and Yahoo.
- **Web push on iPhone:** is a home-screen prompt enough, or do iPhone users rely on email?
- **Card copy for *Your AI's top picks*** on the pick screen: "the top score should get a position" is final as drawn, but "position" is the one trading word there without an explanation.
