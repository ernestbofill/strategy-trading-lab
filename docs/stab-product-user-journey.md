# Strategy Trading Lab: user journey

Oct 14, 2026 · @Ernest

Strategy Trading Lab (the productized STAB) is a strategy lab and a monitoring side app. A new user picks a strategy template, sees how it did in the past, tunes its rules and watches the effect, gives it a budget and a name, and runs it as their own strategy in one of two modes:

- **Simulation**: the app trades on paper, on real prices, and shows how the strategy does from today. Nothing happens outside the app.
- **Live**: the user follows the strategy with real money at their own broker. The app sends each signal, the user says whether they acted on it or skipped it, and the app records those trades and tracks the results.

There is no broker sync in either mode. The app never places orders and never reads the user's broker account: in live mode it only knows what the user confirms. It gives no trading advice. The regulatory background is in `claude/regulation-spain-eu.md`; the wording rules that follow from it are in "Wording rules" below.

The screens are drawn in the [Strategy Trading Lab wireframes · Cyberpunk](../wireframes/), in the [Cyberpunk design system](../design-system/cyberpunk/). The full screen-by-screen spec with final copy, for the implementation agent, is `claude/strategy-trading-lab-app-spec.md`. Where this doc and the wireframes disagreed, the wireframes won (Oct 14) and this doc was brought in line.

## Words we use

The user *picks a strategy template*, sets it up, names it, and *runs* it as a simulation or live. What they run is *their strategy*.

| Word | Means | Where the user meets it |
| --- | --- | --- |
| **Strategy template** | An idea and its rules, as offered in the catalogue, with our tested defaults: "Dip in winners", "Ride the long trend". It is picked, reviewed, backtested and tuned. Templates never change because of what a user does. | Steps 2-3: the template cards, the review pages, advanced settings |
| **Strategy** | The user's own copy of a template, with their name for it, their stocks, settings and budget, running in one mode from the day they start it. It keeps its own trades and results. The user can make any number of strategies from the same template. | From step 4 on: the dashboard, the strategy page, signals, the overview |
| **Mode** | How a strategy runs: **Simulation** (the app makes every trade the rules call for, on paper, at an assumed price) or **Live** (the user trades at their broker and confirms each signal). A strategy's mode is fixed; going live means starting a new strategy. | Step 4, and as a label on every strategy |
| **Signal** | The moment a strategy's rules are met for a stock: a buy step, or, for *Ride the long trend*, a sale. In simulation the app acts on it by itself; in live it asks the user. | Live alerts, the daily overview, the stock rows |
| **Discount zone** | For *Dip in winners*: the range 5-15% below a stock's recent peak, where its rules signal. | Rules, charts, signals, the overview |
| **AI round** | For strategies made from the AI template only: every 2 weeks, the user runs our suggested prompt in their own AI service and pastes the answer back. | Step 9 |
| **AI data sharing** | The user's opt-in to let us keep their AI rounds, anonymised (the AI model and the full answer), to improve the product and compare how AI models score. | Step 9, and Account → Data and privacy |
| **Backtest** | How a template did in the past, with given settings. It belongs to the template and settings, not to a running strategy. | Steps 2-3, *How we tested*, and the strategy page |

**Naming.** On the confirm screen the user names the strategy or keeps the default: the template's name, with a number if they already have one by that name ("Dip in winners 2"). The template's name stays visible under the user's name, with the mode and start date, e.g. "Zscaler & friends · *Dip in winners* · Live · since Oct 13", so the user always knows which idea is running and whether it is real.

## Wording rules

These keep the app on the "information and tools" side of the line between information and investment advice (see the regulation note). They apply to every screen, email and alert.

- **Describe, don't instruct.** Template names describe the situation the rules look for, not an action for the user: *Dip in winners*, *The comeback*, *The big market drop*, *Your AI's top picks*. *Ride the long trend* keeps its name: "ride" is a metaphor, not a buy or sell. Inside a template's page, the strategy is the subject ("It signals when…"), never the user ("Buy when…"). Template copy says the strategy *signals* a buy or a sale rather than that it *buys*.
- **Signals report rules, not opinions.** Every signal says which rule was met, the facts that met it and, for live, what that means under the user's own settings. Never "buy now", no urgency, no price targets or return forecasts.
- **The user owns the setup.** Lists, settings and budgets are presented as the user's choices, starting from our tested defaults: "Choose stocks to watch", "Under your settings", never "recommended for you".
- **No promises.** No claims that markets or stocks will recover or rise. Results are always shown with the comparison to an index fund and the worst year.
- **One disclaimer, everywhere results or signals appear:** "Not financial advice. Signals follow your rules." Where the user commits (the disclaimer accepted once at sign-up, above the Confirm button, and the legal page), the full version: "Not financial advice. Signals follow your rules; you decide and trade at your own broker."

| Where | Before | Now |
| --- | --- | --- |
| Template name | Buy the dip in winners | **Dip in winners** |
| Template name | Buy the comeback | **The comeback** |
| Template name | Buy the big market drop | **The big market drop** |
| Template name | Ride the long trend | Ride the long trend (kept) |
| Template name | Follow the AI's top picks | **Your AI's top picks** |
| Tagline, Dip in winners | Buy the next Nvidia | **Find the next Nvidia** (open point for the legal review: brand name, hints at exceptional returns) |
| Tagline, The comeback | On the rise / Stocks on the rise | **Stocks on the rise** |
| Tagline, Ride the long trend | Ride the wave | **Take the wins, dodge the losses** (open point for the legal review: could read as a promise to avoid losses) |
| Taglines, others | When the market is on sale · Let the AI do the reading | Kept; AI one becomes **Let your AI do the reading** |
| Rules heading | It buys when | **It signals when** |
| Template copy | "the strategy buys…" | **"the strategy signals…"** (a buy, or a sale) |
| Rule term | buy zone | **discount zone** |
| Stock list heading | Stocks we'll watch | **Choose stocks to watch** |
| Fund heading | The fund it buys / trades | **The fund it watches** |
| Stock status | Watching · Buy signal · Building · Holding | Watching · **Signal** · Building · Holding |
| Market-drop idea | "…Broad markets have so far always recovered from them, though sometimes slowly…" | **Claim removed.** The idea stands on its own: wait for the first bounce after a big fall, then signal buys in small steps. |
| Live signal | "Zscaler is in the buy zone, 8% below its recent peak. Your plan: one step of €100 (step 2 of 5)." | **"Your rules for *[strategy name]* are met for Zscaler: 8% below its recent peak, still above its 50-day average. Under your settings that's step 2 of 5 (€100)."** |
| Disclaimer | "For learning and testing, not financial advice. Nothing is traded for you." | **"Not financial advice. Signals follow your rules."** (full where the user commits: "Not financial advice. Signals follow your rules; you decide and trade at your own broker.") |

## Scope and principles

v1 is for learning, testing and following simple strategies. It never tells anyone what to buy or sell.

- **A lab, not advice.** The app shows what each strategy's rules say and how they would have performed. Signals are what the rules say, not recommendations.
- **Two modes.** A strategy runs as a simulation (paper) or live (the user's real trades, confirmed by hand). The mode is always labelled, and results of different modes are never mixed.
- **Templates are ours, strategies are the user's.** We set each template's defaults; once a user makes a strategy from it, the name, stocks, settings and budget belong to the user.
- **No broker sync.** No broker connection, no order placement, no reading the user's account. In live mode the app is a parallel monitor: it knows only what the user confirms.
- **The user confirms, the app records.** For each live signal the user says *I did it* or *I skipped it*. Confirmed trades become the strategy's record; skipped ones are kept too, so the user can see what skipping cost or saved.
- **Signals follow the rules, not the user's trades.** A live strategy's signals come from its rules' own position (its signal trades), the same for anyone running the same rules on the same stocks. The user's confirmed trades only feed their results. (This keeps signals generic, which matters legally; see the regulation note.)
- **Bring your own AI.** The app runs no AI to rate stocks. The AI template gives the user a ready-made prompt to run in their own AI service (ChatGPT, Claude, Gemini or similar); the scores that come back are the user's, and the app applies the strategy's rule to them.
- **AI data only with consent.** We keep AI rounds for product research (the AI model and the full answer) only if the user opts in, separately from the terms of use, and they can withdraw at any time. Nothing that identifies the user is kept with that data.
- **Mostly buy-only.** Strategies define when they would buy. The one exception is *Ride the long trend*, which also signals a sale when the market's trend turns down (all in, all out).
- **One at a time.** Onboarding sets up a single strategy. More are added from the dashboard, each through the same steps, from any template, including ones already in use.
- **Daily, after the close.** All rules are checked once a day after the US market close, as in the POC. Every user gets a short daily overview by email; live strategies also send an alert (web push and email) as soon as there is a signal, to act on the next trading day.
- **Plain language.** Real trading terms stay, each with a one-line explanation, e.g. "SMA 200 (average price over the last 200 days)". The words *ticker* and *tranche* never appear in the app: it says *stock* (or *fund*) and *step*.
- **Simple templates.** Each one fits in a few sentences, has a catchy tagline, and can be understood without trading experience. Each review page leads with the idea behind it, before its rules.
- **Ready out of the box.** Every template ships with suggested stocks and backtested defaults (the POC's).
- **Open about testing.** Every backtest links to *How we tested*: the data, how a test runs, results by period, and what a backtest can't show.
- **Mostly US markets, euro results.** v1 covers the POC's stocks and funds: mostly US-listed, a few listed in Europe and Hong Kong. Budgets, steps and results are in euro, converted at the day's exchange rate; stock prices are shown in the listing's currency, as the user sees them at their broker.
- **Free for now.**
- **Results are measurable.** The user can always see how a template did in the past and how each of their strategies is doing since it started.
- **Visual style:** the Cyberpunk design system, kept as its own artifact.

## 1. Create account

Sign-up takes under a minute and asks only for what the overview and alerts need.

- **Email and password**, with email verification. The POC has a single password; the product needs one account per user.
- **The daily overview** goes by email, on by default.
- **Time zone**, guessed from the browser and editable. The overview is built after the US market close, so the time zone sets when it arrives (early morning in Europe).
- **The disclaimer, accepted once:** "Not financial advice. Signals follow your rules; you decide and trade at your own broker." Plus one line on what the app is: for learning, testing and following strategies, and it never trades for the user.

Live alerts use web push, which the browser asks permission for when the user starts their first live strategy (step 4), not at sign-up. Email is the fallback. On iPhone, web push needs the app added to the home screen; the app says so at that moment.

AI data sharing is not asked at sign-up either: it is asked at the user's first AI round (step 9), where it makes sense.

After sign-up the user lands directly on template selection (step 2), with no empty dashboard.

## 2. Pick a strategy template

The user sees one card per template and picks one (single choice; *Dip in winners* is selected by default). Each card shows the template's name, its tagline, its idea in one or two sentences, and a *Learn more* link. Under the button: "Pick one to start. You can add more strategies later, from any template."

When adding a strategy later from the dashboard, the same screen shows all templates, with "You run 2 from this template" on templates already in use. Nothing is hidden.

The four POC strategies map to four templates; the fifth, the AI template, is new.

| Template | POC strategy | Plain-language idea | Invests in | When it signals | Tagline |
| --- | --- | --- | --- | --- | --- |
| Dip in winners | Leader pullback | A stock on a strong run takes a small step back; the strategy signals that dip. | Individual stocks | A buy in the discount zone, 5-15% below the recent peak, while the longer trend is still up | Find the next Nvidia |
| The comeback | Confirmed turnaround | A stock that fell a lot starts to recover; the strategy looks for a confirmed turnaround. | Individual stocks | A buy when a beaten-down stock climbs 15% above its 50-day average on rising volume | Stocks on the rise |
| The big market drop | Bear-market accumulation | When the whole market falls 20%, the strategy signals buys of a broad S&P 500 fund in small steps as it recovers. | A broad S&P 500 fund | A buy after a 20% fall, once the market bounces 5% off its low; then one step every 2 weeks | When the market is on sale |
| Ride the long trend | Index trend | The strategy signals a buy of a broad index fund when the market moves into an uptrend, and a sale when the trend turns down, to dodge the dip. | A broad index fund | A buy when the market closes more than 3% above its 200-day average; a sale when it closes more than 3% below | Take the wins, dodge the losses |
| Your AI's top picks | new | Every 2 weeks, you run our prompt in your own AI. It reads the news, the mood around each stock and its financial figures, and scores every stock from 0 to 100. The top-scored stock gets a signal for one step. | Individual stocks | After each AI round, one step for the top-scored stock, until the budget is used | Let your AI do the reading |

The effort and risk lines once planned for the cards were dropped to keep them minimal; the risk is covered on the review page.

## 3. Review, test and tune

The picked template opens its review page, already filled in with its defaults, so the user can accept it as is with *Use this template*. Nothing has to be configured. Each template has its own review page: it is the one screen whose content differs a lot between templates.

**What the page shows, in order:**

1. **The idea**: the rationale in plain words, with no numbers and no promises. For example: "Pick winning stocks: companies whose price has climbed steadily for months because the market believes in them. Then wait for a short pullback, when the price dips a few percent." For *The big market drop*: "Big market crashes are rare and scary, and most people sell. Instead of guessing the bottom, the strategy waits for the first bounce and then signals buys in small steps while prices are still low."
2. **The rules**, under "It signals when", each as a plain label and its detail, with every term explained in brackets. Example: "Wait for a discount: it drops into the discount zone, 5-15% below its recent peak." *Ride the long trend* lists a Buy rule and a Sell rule instead.
3. **Choose stocks to watch.** For stock templates, a starter list, each stock with its name and one line on what the company does, all ticked by default and ordered alphabetically; the user can untick any and add their own (US-listed). Under the list: "In our tests, longer lists gave steadier results." For fund templates, the one fund ("The fund it watches"), which the user can change.
4. **Tested.** How the template did in the backtest: the period, how many stocks, the result against simply buying an index fund, and the worst year. Results are shown honestly, including the bad years, with "Past results don't promise future ones." A *How we tested* link opens a page with the data used, how a test runs (daily checks, next day's open, the tested defaults, euro at the day's rate, fees), results by period, and what a backtest can't show (the starter list only holds companies still around today, which flatters past results; real prices can differ from the open).
5. **What can go wrong**, in plain words ("a stock it buys can keep falling").
6. A link to **Advanced settings**.

**The AI template's review page** differs:

- **The idea**: "Nobody can read every news story and financial report about every company, but an AI can. You run our prompt in the AI you already use; it scores each stock, and the strategy signals the top one. You see whether all that reading turns into better picks."
- **The rules**: "1. Every 2 weeks you run the AI round: copy the prompt, paste it into your AI, paste its answer back. 2. The stock with the top score gets a signal for one step, until the budget is used. 3. If you skip a round, there's no signal that round."
- **What the AI reads**: News, Mood and Figures, a multi-select with all three on by default (at least one must stay on). Each one adds or removes a section of the prompt.
- **The prompt**: shown in full, read-only by default, with *Edit prompt* for users who want to change it (with *Reset to suggested*). It is the same for every user, apart from their stock list and the sources they switched on. It asks for scores against stated criteria, never "which should I buy?".
- **What you need**: "An AI service that can search the web (most paid plans can). The app doesn't connect to it and never sees your account."
- **Not tested**: it has no backtest and no *How we tested* link. Its results start on the day the strategy starts, and they depend on which AI the user runs it in.
- **What can go wrong**: "your AI can misread the news, or make things up, and a top-scored stock can still fall."

**Advanced settings** is its own screen. It lists every exact parameter as a sentence with a − / + control ("The discount zone starts 5% below the recent peak"), the tested default under each, and a *Changed* mark on edited ones. *Reset to tested* is always there. The values the user sets here belong to their strategy; the template's tested defaults never change. For the AI template it holds the round frequency (default every 2 weeks) and how many top-scored stocks get a step per round (default 1).

**Every change is tested.** As soon as a setting differs from its default, the screen re-runs the backtest and shows the new result next to the default's, over several time periods rather than one lucky stretch, so the user sees whether the idea got better or worse. (Not for the AI template, which has no backtest.)

## 4. Choose the mode

A screen with two options, *Simulation* selected by default. Intro: "You can start a live copy of a simulation later."

| | Simulation | Live |
| --- | --- | --- |
| In one line | "Test it on paper. No real money." | "Follow it with real money at your own broker." |
| Who trades | The app, on paper, every time the rules say so | The user, at their broker, if and when they choose |
| What the app does | Records every trade the rules call for, at the next day's opening price | Alerts the user to each signal and records what they confirm |
| What the user does | Nothing; read the daily overview | Confirm each signal: *I did it* or *I skipped it* |
| Results show | How the rules do on their own | How the user's real trades do, next to how the rules would have done |

Choosing *Live* adds two things to the setup:

- **Alerts.** The browser asks to allow notifications (web push). Email alerts are on as a fallback, and the only channel if push is refused.
- **A clear line** above the button: "The app never trades for you or connects to your broker. It records only what you confirm."

The mode can't be changed later. On a simulation's page, **Go live with these settings** starts a new live strategy with a copy of its stocks, settings and budget (through steps 4-6, prefilled); the simulation keeps running or can be ended, and the two keep separate histories.

The AI template can run in either mode; both need the user's AI round every 2 weeks (step 9). Its lack of a backtest is restated here.

## 5. Set your budget

The user chooses how much the strategy invests, so results show in euros that mean something to them.

- **Simulation:** a what-if amount. Intro: "No real money moves."
- **Live:** the amount the user plans to put into this strategy at their broker. Intro: "The amount you plan to put into this strategy at your broker. The app doesn't move or hold any money." The step size is the amount shown in each signal; the user can enter a different amount when they confirm.

The screen has the same three settings in both modes:

- **Budget**: the most this strategy can invest in total.
- **Step size**: how much each buy is.
- **Max steps per stock**: buying a stock in steps limits the damage of a bad first buy.

A live summary turns them into a sentence, e.g. "Each buy is €100. A stock gets at most 5 steps (€500). The budget covers 30 steps, enough for 6 stocks bought in full.", with a small row of squares, one per step. **When the budget is used up, the strategy stops signalling buys.** In live mode, only confirmed trades use the budget; skipped signals don't.

| | Default |
| --- | --- |
| Budget | €3,000 |
| Step size | €100 |
| Max steps per stock | 5 |
| Steps the budget covers | 30 |
| Stocks it can buy in full | 6 |

We suggest the initial defaults, the same for everyone; after that, every amount is the user's to change. Each strategy keeps its own budget. *Ride the long trend* buys the whole budget at once, so for it only the budget applies. For the AI template with these defaults, one step every 2 weeks uses the budget in about 60 weeks.

Amounts are in euro. Buys of dollar stocks are converted at the day's exchange rate, and the strategy's results include the effect of the euro-dollar rate, as a euro investor's real results would.

## 6. Name, preview and confirm

A last screen sums up the setup before it starts, with the **name** at the top: an editable field prefilled with the template's name (numbered if the user already has one by that name), with a live preview of how it will show ("[name] · [template] · Live · from today"). Below it: the template, the mode (Simulation or Live), the stocks, the settings (tested defaults or changed), the budget, the daily overview (by email, after the market close) and, for live, the alerts (app and email). For the AI template it also shows the prompt (suggested or edited) and that the first AI round is due now. Each line has a link back to its screen to change it.

It also says what happens next:

- **Simulation:** the user sees right away how the template did in the past; from today the strategy runs on real prices and records its own signal trades; the first daily overview arrives tomorrow. Button: **Start the simulation**.
- **Live:** the user sees right away how the template did in the past; from today the app watches the stocks and alerts the user as soon as their rules are met; the user confirms each signal, and the app tracks the results of what they did. Button: **Start following it live**.
- **AI template (either mode):** no past results; the first AI round is due now, so the next screen is the AI round (step 9).

The full disclaimer ("Not financial advice. Signals follow your rules; you decide and trade at your own broker.") sits just above the button. The button ends onboarding and opens the dashboard (or, for the AI template, its first AI round).

## 7. Get your daily overview

Once a day, after the market close, the user gets one short overview by email covering all their strategies. It reads like a briefing: it says what each strategy's rules currently show, not what the user should do.

1. **The overview arrives** by email at a time that suits the user's time zone.
2. **Waiting for you** (first): live signals the user hasn't confirmed or skipped yet, and AI rounds that are due, each with a link.
3. **What changed today.** A stock started or stopped meeting a strategy's rules, in plain words: "ZS fell 8% from its recent peak and is still above its 50-day average (average price over 50 days). That meets the rules of your strategy *Zscaler & friends*." For simulations, also the signal trades made today.
4. **Close to the line.** The stocks nearest to meeting each strategy's rules, with how far away they are.
5. **The scoreboard.** One line per strategy, with its name and mode, and its results so far (step 10).
6. **Every item links to its page**, with the chart and the rule explained.

The overview never uses buy or sell as an instruction. It states what the rules currently say, and the disclaimer sits at the bottom.

## 8. Live: act on a signal and confirm

This step exists only for live strategies. It is the core loop of live mode.

1. **The signal arrives** as a web push notification and an email, right after the daily check that found it (after the US market close), or right after an AI round for AI strategies. It says which rule was met and why, under the user's settings: "Your rules for *Zscaler & friends* are met for Zscaler: 8% below its recent peak, still above its 50-day average. Under your settings that's step 2 of 5 (€100)." For *Ride the long trend*, sell signals arrive the same way: "Your rules for *[name]* are met for a sale: the market closed 3.2% below its 200-day average."
2. **The user decides at their broker**, the next trading day. The app doesn't link to a broker or prefill an order.
3. **The user confirms in the app**, from the alert, the *Waiting for you* list or the stock row, with one of two answers:
   - **I did it.** A short form: date, price, amount (or number of shares) and an optional fee. The price is prefilled with the **next trading day's opening price** once the market has opened (before that, the field is empty with the signal day's close shown as a reference). The amount is prefilled with the step size. The user corrects what differs and saves. The app records the trade and updates the budget used, the steps and the results.
   - **I skipped it.** One tap, with an optional reason ("Price ran away", "Didn't trust it", "No cash", "Other"). The app records the skip; no budget is used.
4. **Unanswered signals** stay in *Waiting for you* on the dashboard and in the overview, and get one reminder. A signal can still be answered later (the user enters the real date); after the signal ends, it is marked *Missed*.
5. **The user can also log a trade by hand** on a stock (e.g. they bought before the alert came, or sold for their own reasons), and edit or delete a trade they entered wrongly. Manual trades are marked as such, since they weren't a strategy signal.

**Signals follow the rules, not the user's trades.** After a skip, the rules carry on as if the step had been bought (their signal trades), so the next signal for that stock is step 3 of 5, and a *Ride the long trend* sell signal arrives even if the user never bought. The strategy page shows both: what the rules hold and what the user holds.

Only *Ride the long trend* has sell signals. In the buy-only templates, if the user sells at their broker, they log the sale by hand so the live results stay right.

## 9. AI template: run the AI round

Every 2 weeks (the default; the user can change it in advanced settings), a strategy made from the AI template needs the user's input. The app runs no AI itself: it prepares the prompt, the user runs it in their own AI service, and the app reads the scores back.

1. **The round is due.** A notification and a *Waiting for you* item: "Time for the AI round of *[strategy name]*. It takes about two minutes."
2. **Copy the prompt.** The screen shows the prompt, filled in with today's date, the user's ticked stocks and the sources they switched on, with one *Copy prompt* button. The prompt asks the AI to:
   - search the web for recent information (with a date window matching the round, e.g. the last 2 weeks, plus the latest quarterly figures), so it doesn't rely on what it remembers;
   - score each stock from 0 to 100 on each switched-on source (News, Mood, Figures) and overall, by stated criteria;
   - give a one-line reason and its main sources, with dates, for each score;
   - answer in a fixed format the app can read (a table or JSON block, one row per stock).
   It never includes anything about the user (no budget, goals or holdings), and it asks for scores, never "which should I buy?".
3. **Run it in your AI.** The user pastes the prompt into the AI service of their choice and waits for the answer. Optional *Open in…* links open common AI services in a new tab, without connecting to them.
4. **Paste the answer back and name the AI model.** The user pastes the whole answer into one box and picks the AI model they used: a list of common services and models (e.g. "ChatGPT · GPT-5", "Claude · Opus", "Gemini · 2.5 Pro") plus *Other*, with a free-text field. It is prefilled with the model from their last round. The app reads the scores block and checks it: every stock scored, scores from 0 to 100, no extra stocks. It shows the result as a table, headed "Your AI's scores" (stock, overall score, the AI's one-line reason), and points out anything missing or unreadable, so the user can ask their AI again.
5. **Confirm the scores.** The user taps *Use these scores*. The top-scored stock gets one step, under the usual rules: max steps per stock and the budget.
   - **Simulation:** the app records the signal-trade buy at the next day's open and returns to the dashboard.
   - **Live:** the step arrives as a signal to confirm or skip, as in step 8; the app opens it straight away.
6. **Skipped rounds.** If the user doesn't run a round within a week of its due date, it is marked *Skipped* and there's no signal. The next round comes 2 weeks after the due date.

Every round is kept in the strategy's history, for the user: the date, the prompt used, the AI model, the full answer, the scores and the AI's reasons. That way the user can see, over time, whether the AI's top picks did better than the rest of their list, which is the point of the template.

### AI data sharing (opt-in)

At the user's first AI round, before *Use these scores*, a short consent box asks whether we may keep their AI rounds to improve the product. It is off by default, separate from the terms of use, and the round works the same either way.

- **Copy:** "Help us learn which AIs score stocks best. Share this round anonymously: the AI model you used and its answer. We never keep your name, email, budget or trades with it. You can stop at any time in Account → Data and privacy." Checkbox: "Share my AI rounds anonymously". A *What we keep* link opens the full list.
- **What is kept, per round, when sharing is on:** the AI model, the date, the prompt version and whether the user edited it, the stock list and the sources switched on, the full answer as pasted, and the parsed scores. Later stock prices are joined from market data, not from the user.
- **What is never kept with it:** the user's name, email, account ID, strategy name, budget, step size, confirmed trades or whether they acted on the round.
- **The full answer is stored as text, never run or followed.** Before storing it, the app removes anything that looks like an email address, phone number or URL with personal parameters, in case the user pasted more than the AI's answer.
- **The choice is remembered** for later rounds and shown as one line on each round screen ("Sharing anonymously · Change"), so the user can switch it off for a single round or for good.
- **Withdrawing** stops future sharing. Rounds already shared can't be traced back to the user, so they can't be removed one by one; the *What we keep* page says so before the user opts in.

**What we do with it.** First, internally: compare AI models on the same stocks and dates, improve the suggested prompt, and check how reliably each model follows the answer format. Later, as a lab feature for users: an **AI comparison** page showing, by AI model, how its top-scored stocks did against the rest of the lists it scored, with how many rounds it is based on. It compares AI models, never stocks: the page doesn't show which stocks AIs rated highest, so it doesn't turn into a published stock ranking (see the regulation note).

## 10. Follow the results

**Simulation results** come from the strategy's rules applied to market prices, with its budget, since the strategy started. Signal trades use an assumed price (the next trading day's open), stated on the page. The template's backtest is shown alongside, for the past.

**Live results** come only from the trades the user confirmed, at the prices they entered. Next to them, the page shows how the rules would have done on their own, with the same budget, so the user sees the gap: "You: +6.2% · The rules on their own: +8.9% · Index fund: +4.1%". Skips and missed signals explain most of the gap, so they're listed.

**AI strategy results** also show how the AI's picks did against the rest of the user's stock list since each round ("Top-scored picks: +5.1% · The rest of your list: +2.3%"), and, once the AI comparison page exists, a link to how other AI models have done.

All results are in euro, including the effect of the euro-dollar rate.

**Dashboard (home).** The app's main page. At the top, *Waiting for you* lists unanswered live signals and AI rounds that are due, each linking straight to its screen. Below, one card per strategy: the user's name for it, the template's name, a *Simulation* or *Live* label, the result since start, the same money in an index fund, a small chart, and the budget used. A card with something waiting shows a small dot by its mode label; the detail stays in *Waiting for you*, so it isn't repeated. A strategy started today has no result yet, so its card shows the template's past (backtest) result instead (AI strategies show "No results yet"). Below the cards, *+ New strategy* runs steps 2-6. The disclaimer sits at the bottom.

**Strategy page.** A card opens its page: the name (editable), the template and mode, its result since start against the index (and, for live, against the rules on their own), the budget used, how many signals were acted on, skipped or missed (live), and one row per stock with its status (Watching, Signal, Building, Holding; live adds *Waiting for you*), the steps bought and its own result. For live strategies each row shows what the rules hold and what the user holds when they differ. AI strategies also show each stock's latest score. Each row opens that stock's page. *Edit list* changes the stocks the strategy watches: removing a stock stops its signals, and the user's trades in it stay in the results. Links: *Tune the settings* (the advanced settings), *Backtest* (for AI strategies: *AI rounds*, the round history), and for simulations *Go live with these settings*. Strategies can be paused or ended from here (not designed yet). The disclaimer sits at the bottom.

| Metric | In plain words | Simulation: built from | Live: built from |
| --- | --- | --- | --- |
| Budget used | How much of the budget is invested, e.g. "€1,200 of €3,000 · 12 of 30 steps" | Signal-trade buys vs budget | Confirmed buys vs budget |
| Average cost | What was paid on average per share | Signal-trade buys | Confirmed buys, with fees |
| Current value | What the position is worth today, in euro | Signal-trade buys, latest price and exchange rate | Confirmed trades, latest price and exchange rate |
| Gain or loss | Current value minus what was invested, in money and % | Same | Same |
| Versus the index | The same money bought into a broad index on the same days | Signal-trade buys and index prices | Confirmed buys and index prices |
| Versus the rules | What the rules would have made on their own | – | The rules' own position (signal trades), which also drives the signals |
| Followed signals | How many signals the user acted on, skipped or missed | – | Confirmations |
| Winning buys | How many buys are ahead of their cost today | Signal-trade buys and latest prices | Confirmed buys and latest prices |
| Picks vs the rest (AI only) | How the top-scored stocks did against the unpicked ones since each round | AI rounds and prices | AI rounds and prices |
| Next up | Stocks closest to meeting the strategy's rules, with the distance | Daily check | Daily check |

Still to design:

- **Simulation variants** of the budget, confirm and strategy screens, and the live result of an AI round.
- **Stock page:** the chart with the discount zone shaded, the status history and the trades for that stock (live: confirmed, skipped and manual, each marked; rules' position vs the user's).
- **Stock list editor** from the strategy page, stock search, fund picker, and the prompt editor.
- **Manual trade form**, and editing or deleting trades.
- **Sell signals** and the "out of the market" status for *Ride the long trend*.
- **AI round history** and the error state when an answer can't be read.
- **AI comparison page** (later): results by AI model.
- **Account**, including **Data and privacy**: AI data sharing on or off, *What we keep*, export and delete account.
- **Renaming** on the strategy page.
- **Go live with these settings** from a simulation.
- **Pausing or ending** a strategy.
- **Backtest page.**
- **History:** every signal, trade, skip, AI round and status change, in date order.

## Decided (Oct 13)

- **Naming:** the catalogue holds *strategy templates*; what the user runs is *their strategy*, named by them or after the template. Simulation and live are its *mode*.
- **Several strategies per template:** yes, any number. Templates are never hidden on the pick screen.
- **Switching modes:** no switching in place. *Go live with these settings* starts a new live strategy from a simulation; histories stay separate.
- **Live position vs the rules:** signals follow the rules' own position (signal trades), not the user's real trades (no preference given; chosen because it keeps signals generic, per the regulation note).
- **Signal timing:** once a day, after the US market close, as in the POC. Users act the next trading day.
- **Live alert channels:** web push (PWA) and email. Telegram is not in v1.
- **Prefilled price on "I did it":** the next trading day's opening price.
- **AI template defaults:** a round every 2 weeks; the top 1 stock gets a step.
- **AI model and answer:** each AI round asks for the AI model used and the full answer.
- **Comparing AIs:** yes. With the user's opt-in consent, we keep their AI rounds anonymised (the AI model and the full answer) to improve the product and, later, to show users how AI models compare. Comparisons are by AI model, never a ranking of stocks.
- **Markets:** the POC's stocks and funds in v1: mostly US-listed, a few in Europe and Hong Kong (see `backend/src/StrategyTradingLab.Api/Templates/TemplateAssets.cs`).
- **Currency:** budgets, steps and results in euro at the day's rate; stock prices in dollars.
- **Wording:** see "Wording rules": template names describe rather than instruct (*Dip in winners*, *The comeback*, *The big market drop*, *Your AI's top picks*; *Ride the long trend* kept); taglines "Find the next Nvidia" and "Stocks on the rise"; "discount zone" instead of "buy zone"; "Choose stocks to watch"; status "Signal"; signals phrased as "Your rules for … are met"; the market-recovery claim removed; one short disclaimer everywhere.

## Decided (Oct 14, from the wireframe review)

- **Signals wording:** rules sit under "It signals when", and template copy says the strategy *signals* a buy or a sale.
- **Ride the long trend tagline:** "Take the wins, dodge the losses".
- **How we tested:** every backtest links to a page explaining the test.
- **Longer lists:** stock templates note "In our tests, longer lists gave steadier results."
- **Dashboard:** *Waiting for you* stays at the top; cards show a dot instead of repeating the item.
- **Edit the stocks of a running strategy:** yes. Removing a stock stops its signals; trades in it stay in the results.
- **AI round in simulation:** *Use these scores* records the signal-trade buy and returns to the dashboard; in live it opens the signal to confirm.
- **Wireframes win:** where this doc and the wireframes disagreed, this doc was updated to match.

## Open questions

- [ ] **Regulation.** See `claude/regulation-spain-eu.md`. Live signals stay outside "investment advice" only if they are the result of rules the user chose, use nothing about the user's circumstances and aren't presented as suitable. The wording rules above support that. For the legal review: "Find the next Nvidia" (brand name, hints at exceptional returns), "Take the wins, dodge the losses" (could read as a promise to avoid losses), the suggested AI prompt, the default stock lists (still our content), and the AI comparison page (results by AI model, with no stock ranking).
- [ ] **Anonymous or pseudonymous?** Under GDPR, data is only anonymous if it can't be linked back to the user by anyone, including us. Keeping shared rounds in a separate store with no account ID (as above) gets close; anything that keeps a link, even internally, is pseudonymous and stays personal data, with access and deletion rights. Decide which before building, and word the consent box to match. The consent text and privacy policy should get the same legal look.
- [ ] **Edited prompts.** Rounds from users who edited the prompt are less comparable. Keep them in the comparison, keep them apart, or leave them out?
- [ ] **The suggested prompt.** Who writes and maintains it, how is it versioned (users who edited theirs keep their own), and how do we test it across ChatGPT, Claude and Gemini so the answer format is reliable? The POC's 0-10 news sentiment scoring is a starting point for the criteria. The shared rounds will show which models follow the format.
- [ ] **Reading the answer.** How forgiving is the parser (table vs JSON, extra text around it, a stock name instead of its symbol)? Pasted text is treated as data only: the app reads numbers and short reasons from the scores block and never acts on anything else in it.
- [ ] **AI model list.** Which services and models are in the picker, and who keeps it current as new models ship?
- [ ] **Advanced settings for each template.** Only *Dip in winners* is drawn. Which POC parameters does each template expose, and how? (The POC's recent peak is a 20-60 day window; the screen shows one number, default 60.)
- [ ] **Price data.** Which provider supplies daily US prices and the euro-dollar rate, with terms that allow commercial use, and what does it cost at many users, given the app is free? The POC uses eToro and Yahoo.
- [ ] **Web push on iPhone.** It only works once the app is added to the home screen. Is a home-screen prompt enough, or do iPhone users rely on email alerts?
