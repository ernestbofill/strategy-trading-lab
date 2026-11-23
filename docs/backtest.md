# Backtest

How STL's own backtest works: what it tests, against what, with which money, costs and stocks, and which data. Planned with Ernest from 2026-11-19 to 2026-11-23. It uses the wording of the app-wide copy change in issue #277 (*amount per trade* and *trades* for what the app calls a step today). The screens are drafted below (*Screens, draft*), for another round with Ernest.

Today's template figures come from the POC's research ([backtest-research-findings.md](backtest-research-findings.md)). This backtest replaces them with STL's own engine, so the tested results describe the rules the app runs.

## Principle: the same money, the same costs

A test is fair only if the strategy and every baseline get the same money, on the same schedule, with the same costs, and are measured with the same formulas. Everything below follows from that.

The backtest runs the app's own engine (`Engine/`): the same rules, signals at the close, each trade at the next trading day's open, in the strategy's currency at each day's exchange rate (the baselines too). A rule the app changes, the backtest changes with it, so a result always describes what the app would have done.

**Checked against the app:** a backtest of a running simulation, with its settings and list from its start day, must give the same signals and trades the app recorded. It is the simulator's main test.

## What is tested

| Parameter | Default | Notes |
| --- | --- | --- |
| Rules | The template's tested defaults, or a strategy's settings | Any Advanced settings values |
| Stocks | The test draft: one fixed draft of 60 stocks from the S&P 500 | Or the strategy's own list, or one stock (see *Stocks*) |
| Index | S&P 500 | *Bear markets* and *Index trend* only: the index fund they trade (S&P 500, Nasdaq-100, DAX, Nikkei 225…). How often a market fell 20% depends on it |
| Years | 16 years, in 4-year periods (Ernest, 2026-11-23) | Both are parameters. 4 years gives the strategy time to profit from its buys. One stock and the strategy's own list: the latest period only |
| Budget | €6,000 (60 trades, one per drafted stock) | A test setting. A strategy's test uses its own budget. One stock: max trades × amount per trade, so the budget fits it |
| Amount per trade | €100, up to 5 trades a stock | As the strategy's budget |
| Monthly limit | €400 a month (the budget over 15 months at the earliest) | A cap on new money spent; unspent limit carries over in full (see *Money*). Limit = budget means no limit |
| Fee | 0.1% a trade | No fixed part, so results don't depend on the budget's size, only on how many trades it holds |
| Spread | 0.1% a trade | Added to the buy price and taken off the sell price |

No currency fee. Taxes are not included.

## Periods

- The periods are calendar years, counted back from today: with the defaults, 2011–14, 2015–18, 2019–22 and 2023–26 (to the last close). The data starts in late 2009, for the first period's warm-up.
- Each period is an independent test that starts with a fresh budget: the strategy tested in different periods, as *How we tested* says today.
- **All** is the unified result, the periods combined (Ernest, 2026-11-23): the average of the periods' rates, the worst year of any period, the average budget invested, the trades added up. Each period's own results stay available (the period tabs). It is not one continuous test: with the budget used in the first two years, that would mostly measure holding.
- **Warm-up:** the rules read about 15 months of prices before a period starts (the SMA 200, the 52-week high and the 168-day low need them), but nothing is bought before the start. It is what a strategy started that day in the app would see. Without it, the first year of each period would have no signals.
- At a period's end, holdings are valued at the last close. Nothing is sold to close the test, for the strategy or the baselines.
- The latest period runs to the last close, so it is shorter than the others (2023–26 is under 4 years today); its label says so.
- With 16 years, 2008 and 2000–02 are out (2011 brings a fall of almost 20%). On the test draft the period can't go past the dataset's years; reaching back further means a longer dataset, where more former members have no prices. The strategy's list and one stock can go back as far as Yahoo has their prices.

## Money

- The test account starts with the whole budget in cash. Every line, strategy and baselines, spends at most the monthly limit of new money in a calendar month.
- **Unspent limit carries over in full** (Ernest, 2026-11-23: the simplest): by the end of month *m*, at most *m* × the limit is spent in total. A quiet stretch saves room for a busy one, up to the budget. Baselines 1 and 3 spend the limit every month anyway.
- Sale proceeds are reinvested at once, outside the limit. Only *Index trend* sells: each month it buys with that month's limit plus any money left from sales.
- When several signals on the same day need more money than is left, they are paid in a seeded random order (the same seed gives the same result, with no alphabetical bias). The ones that can't be paid follow the app's current rule (below).

**A signal with no money** (accepted for now, to change later). Today, when the budget can't pay a step, the app makes no signal but the rules count the step as bought, as the POC's backtest does ("as if the step had been bought"). So a comeback's first step is then gone, and *Bear markets*' step counter moves on. The backtest follows that rule in v1. Ernest's rule for later, for the app and the backtest together: a signal with no money waits, and buys once money is there while its window is still open (*Bear markets*: its step stays due while the conditions hold). See `ROADMAP.md`.

## Baselines

Decided by Ernest on 2026-11-22. Every baseline pays the same fee and spread, never sells, and is valued at each period's last close. The *pool* is the stocks the test uses: the draft, the list or the stock.

| # | Baseline | What it does | The question it answers |
| --- | --- | --- | --- |
| 1 | **S&P 500, monthly** | Buys SPY with each month's limit until the budget is used. | Does the strategy beat the market? (The product rule's index fund.) |
| 2 | **Same days, same money, same list** | Each time the strategy buys, the same amount goes into the whole pool that day, split evenly. | Does the strategy choose the right stocks in the list? Same days and money, so only the choice of stocks differs. |
| 3 | **Same stocks, monthly** | Buys the whole pool with each month's limit, split evenly, until the budget is used. | Does the timing matter much? Read against baseline 2: both buy the whole pool, only the days differ. |

So the gaps read as: strategy vs 1, against the market; strategy vs 2, the choice of stocks; 2 vs 3, the timing.

- With the limit equal to the budget, baselines 1 and 3 buy everything on the first day.
- Baseline 2 needs a pool of more than one: for one stock, and for *Bear markets* and *Index trend* (one fund), it is the strategy itself, so it isn't shown. For these two, baseline 3 is the same fund, monthly.

## Results

Per period and for **All**, for the strategy and each baseline, all **money-weighted** (Ernest, 2026-11-22): each euro counts for as long as it was invested, so early buys weigh more.

- **A year, on average**, on the money invested.
- **Worst calendar year**, money-weighted too.
- **Budget at work** ("Budget invested over time"): "On average, [n]% of the budget was invested". For the strategy, and next to each baseline. The formula, over the period's trading days:

  `budget invested = (1 / N) × Σ min(1, invested on day t / budget)`

  where *N* is the number of trading days in the period and *invested on day t* is the money put into the holdings still held that day (their cost, not their value). The whole budget invested on the first day reads 100%; money invested on the last day adds almost nothing. Example: half the budget on the first day and the other half at the period's middle reads 0.5 × 100% + 0.5 × 50% = 75%. After a sale (*Index trend*) the money is idle until bought again; reinvested gains can't take a day above 100%.
- The number of trades.
- **A note that idle money isn't counted:** "Money-weighted returns (IRR) on the money invested: idle cash is left out." (Not *direct alpha*: that is the gap to an index bought with the same cash flows, which baseline 2 and the dropped *S&P 500, same days* measure.) Roughly, the rate on the whole budget would be the rate shown times the share invested.

The product rules hold: every result sits next to the index fund and the worst year.

## The dataset and the draft

Two different things:

- **The dataset** is everything a test can draw from: every stock that was in the S&P 500 at some point in the years tested (with its dates in the index), and their daily prices. One snapshot a month (see *Data*). Index funds aren't in it: their prices are fetched when a test runs.
- **The test draft** is the stocks a test uses: one fixed draft, drawn from the dataset's S&P 500 members on each period's start date (the same seed every time). Tuning and comparing always use it unless the strategy's list or one stock is chosen, so a difference comes from the settings, not from the stocks. There is no other draft (Ernest, 2026-11-22: random draws dropped).
- **Its size: 60** (Ernest, 2026-11-23), the most stocks a strategy can follow: large enough that the strategies find candidates in it and one lucky or unlucky stock weighs little.

**How it is drawn** (Ernest, 2026-11-23):

1. **Eligible:** the S&P 500 members on the period's start date with prices from the warm-up on.
2. **By sector:** each of the 11 sectors (Yahoo's, as for added assets) gets seats in proportion to its share of the eligible members.
3. **By size, within each sector:** low, middle and high thirds by average daily traded value (price × volume over the 60 trading days before the start), a size measure that exists for any past date.
4. **Drawn** at random within each sector and size group with a fixed, published seed. Each period gets its own draft, from its own members, with the same seed.
5. **Checked, never re-drawn to look better:**
   - *Representative:* per period, the draft's *same stocks, monthly* rate is within about ±2 points a year of the same baseline on all eligible members.
   - *Candidates:* each stock template, at its defaults, has 4–5 different candidates in it every calendar year (Ernest, 2026-11-23). The report shows the count per template and year, with the trades and the budget invested.
   A failed check is reported and decided with Ernest (a larger draft, for example); changing the seed until the numbers look right would be cherry-picking.

## Stocks

Three ways to choose the pool:

1. **The test draft** (the default, for comparing): drawn from the S&P 500 as it stood on each period's start date (the stocks SPY held then), stratified by sector, with a fixed, published seed. The same draft and data make results of different strategies and settings comparable.
2. **The strategy's own list.** Over less time (default: the latest period only), since a list chosen today knows how its stocks did, which flatters the past. Not limited to the dataset's years. A stock without enough history at a period's start sits that period out, and the result says so.
3. **One stock.** The budget is sized for it (max trades × amount per trade). The latest period only, by default, as for the own list: the stock is chosen knowing how it did.

Not for *Your AI's top picks*, which has no rules to replay.

A stock may only be drawn if it was in the index on the period's start date and has prices from the warm-up on. Drawing from today's members would only pick the survivors.

## Data

- **Index membership:** the S&P 500 on any date, from [fja05680/sp500](https://github.com/fja05680/sp500) (MIT licence, since 1996, updated every couple of months), checked against a second point-in-time list where both exist. Renamed tickers (FB → META) need a symbol map.
- **Prices:** Yahoo's daily prices, adjusted for splits and dividends, as the app uses today. A test on the strategy's list, one stock or an index fund (*Bear markets*, *Index trend*, and SPY for baseline 1) fetches those prices from Yahoo when it runs (kept for reuse), so it isn't limited to the dataset's years; a test on the test draft is. Dividends therefore count as reinvested in the same stock, before tax. *Pending:* the 15% US withholding tax on dividends isn't deducted.
- **Survivorship:** Yahoo keeps no history for delisted stocks, so former members that were delisted can't be drawn. Each period's report counts them ("31 of 503 members had no prices"), so the bias shows. Only a paid data source with delisted stocks would remove it.
- **A snapshot a month**, not daily: one compressed file per ticker plus a manifest (membership, symbol map, missing tickers), in the backend's object storage bucket, not in the `prices` table. Each result names the snapshot it used. Yahoo re-adjusts past prices after dividends, so a test re-run on a newer snapshot can move slightly; accepted (Ernest, 2026-11-20).
- **A quality report per snapshot**, on an owner page: members and how many have prices at each period's start, gaps over 5 trading days, one-day moves over 40% (usually a split error), renamed and missing tickers. The first report is reviewed before any result counts.

## Where it lives

Decided by Ernest on 2026-11-23. All owner-only.

- **The Backtest page**, its own page: the test's settings (stocks, index, period, money, costs) and the full results. It is where the *test* is tuned; the strategy's settings are tuned in Advanced settings.
- **Entry points**, each opening the page preset:
  - the strategy page's *Backtest* tile: the strategy's settings and budget on the test draft;
  - Advanced settings, on a running strategy or while starting one: the settings on screen;
  - *Review a template* (step 2 of a new strategy): the template's defaults on the test draft;
  - the stock page: *Test this stock*, one stock under the strategy's settings, with the budget sized for it.
- **A panel in Advanced settings**: a preview of the results with *Rerun tests* on the test defaults (the test draft, the default periods and money), with *Default / Saved / Current*, the average of the periods and one baseline, so tuning the strategy rarely needs the page.
- *How we tested* shows the template figures from the backtest (ROADMAP item 56).
- No *Saved tests* in v1 (comparing strategies side by side): later, if missed.

## Screens (draft)

Second round with Ernest on 2026-11-23. Text wireframes at 390px; the numbers are made up. Every *i* is an info tip (the app's `InfoTip`).

**A · Backtest page: test settings** (top of the page, collapsed; *Edit* opens the form in place)

```
← BACK                              Backtest
Winners at a discount · My dip list
Rules: your saved settings · 2 changed
                              [ Edit rules ]

┌ TEST SETTINGS ─────────────────── [Edit] ┐
│ Stocks   Test draft · 60 from the S&P 500│
│ Years    16, in 4-year periods            │
│ Money    €6,000 · €100 per trade · 5 a   │
│          stock                           │
│          at most €400 a month            │
│ Costs    0.1% fee · 0.1% spread a trade  │
│ Data     2009-10 to 2026-10 · snapshot   │
│          2026-11                         │
└──────────────────────────────────────────┘

Expanded:
STOCKS   (•) Test draft · 60 from the S&P 500
         ( ) This strategy's list · 12
         ( ) One stock        [ NVDA      ▾ ]
INDEX    [ S&P 500 ▾ ]   (Bear, Trend only)
YEARS    [ 16 ], in [ 4 ]-year periods
MONEY    Budget [ 6,000 ]  Per trade [ 100 ]
         Up to [5] trades a stock
         Each month at most [ 400 ]
         Unspent room carries over.
COSTS    Fee [0.1]%   Spread [0.1]%  a trade
[ Reset to test defaults ]    [ Run backtest ]
```

- *Edit rules* opens Advanced settings for this strategy (or the draft's).
- **Years and data:** with the test draft, the years can't reach past the dataset (16 years and their warm-up, shown on the *Data* line). The strategy's list and one stock aren't limited to the dataset: their prices come from Yahoo for those stocks alone, as far back as Yahoo has them; their default stays the latest period.
- No warning on the strategy's list or one stock (Ernest: say nothing); the latest-period default does the work.

**B · Backtest page: results**

```
RESULTS                         ran 23 Nov, 10:42
[2011-14][2015-18][2019-22][2023-26][ All ]

                    A year,   Worst   Budget
                    average    year   at work i
Your strategy        +11.6%    -17%      58%
1 S&P 500, monthly i  +9.1%    -19%      81%
2 Same days, list  i  +9.9%    -16%      58%
3 Same stocks, mthly i +9.8%   -18%      81%

52 trades · test draft: 60 stocks, 2 without prices
Money-weighted returns (IRR) on the money
invested: idle cash is left out.
                         See the 52 trades →

 Value of the money invested
 €  ╭────────────────────────────────╮
    │   ── strategy   ·· S&P 500     │
    │   (tap a baseline to add it)   │
    ╰────────────────────────────────╯

What the gaps say
vs 1  the market            +2.5 a year
vs 2  the choice of stocks  +1.7
2 vs 3  the timing          +0.1
```

- **Info tips:**
  - *1 S&P 500, monthly*: "Buys an S&P 500 fund with each month's money until the budget is used. Does the strategy beat the market?"
  - *2 Same days, same list*: "Each time the strategy buys, the same amount goes into all the stocks in the list that day, split evenly. Does the strategy choose the right stocks?"
  - *3 Same stocks, monthly*: "Buys all the stocks in the list with each month's money, split evenly. Does the timing matter? Compare it with 2: same stocks, other days."
  - *Budget at work*: "How much of the budget was invested, on average over the period's days. All of it spent on day one is 100%; all of it spent 2 years into a 4-year period is 50%."
- **All** tab: the average of the periods' rates, the worst year of any period, the average budget at work; under it, "Average of 4 periods, each a fresh test." The latest period's tab says it runs to the last close.
- **See the trades**: the strategy's simulated trades in the shared data table (`DataTable`): date, stock, buy or sell, trade number (2 of 5), price, amount, fee; sortable and filterable, for the selected period.
- **The chart**: the strategy and baseline 1; tapping a baseline row adds its line.
- **One stock**: baseline 2 is hidden (it would be the strategy itself); baseline 3 reads "[stock], monthly". **Bear markets** and **Index trend**: baseline 2 hidden, baseline 3 is the index fund, monthly.

**C · Advanced settings: the preview panel** (bottom of Advanced settings, running or new strategy; the test defaults; no periods, only the average of the periods)

```
BACKTEST                      [ Rerun tests ]
Test draft · 16 years in 4-year periods · €6,000
⚠ Settings changed since the last run.

                   Default   Saved  Current
A year, on average  +10.8%  +11.0%  +11.6%
Worst year           -14%    -15%    -17%
Budget at work i      48%     51%     58%
Trades                 41      45      52
S&P 500, monthly i   +9.1%   (the same for all)

Money-weighted (IRR) on the money invested.
                  Open the backtest page →
```

- *Saved* only shows when the saved settings differ from the defaults (on a new strategy, nothing is saved: *Default · Current*). On the defaults with nothing changed, one column and "You're on the tested defaults. Change a setting and rerun to compare."
- One baseline only, *S&P 500, monthly* (the market); the others are on the page.
- While running, the button reads "Running…" and the table dims.

**D · Entry points**

- *Review a template*, under the *Tested* box: "How we tested →" and "Backtest these rules →".
- The strategy page's tile, brought back: "Backtest" / "Test these rules".
- The stock page: "Test this stock →" with "One stock under this strategy's rules."

**E · Backtest data** (owner page, `/admin/backtest-data`): the snapshot and *Refresh*; members and how many have prices at each period's start; missing and renamed tickers (lists); the test draft's two checks (representative, candidates per template); warnings (gaps, jumps), each with ticker and date.

## Biases

| Bias | How the backtest handles it |
| --- | --- |
| Look-ahead in prices | Signals at the close, trades at the next open; the warm-up reads only past prices |
| Look-ahead in the stocks | The S&P 500 as it stood at each period's start; the own list and one stock default to the latest period |
| Survivorship | Counted and shown per period; removed only by a paid data source |
| Tuning to the past | Every period is shown, not only All |
| Luck of the draw | One large draft, stratified by sector; the strategy's own list as a second view |
| Unequal money or costs | The same money, limit, costs and formulas for every line |
| Backtest ≠ app | The same engine; simulations use the same costs; the monthly limit becomes a strategy setting |

## In the app (decided, not built)

- **The monthly limit becomes a strategy setting:** "Spend at most [€x] a month", part of the budget, with unspent limit carried over in full as in the backtest. Existing strategies get x = the budget, so nothing changes for them, and it can be lowered afterwards (with *Change the budget*).
- **Simulations use the backtest's costs:** fee 0.1% + spread 0.1% a trade, no fixed part, for new trades from the change on (past trades keep the fee they were recorded with). Manual strategies keep the fee the user enters (`ROADMAP.md` item 41).

## Open points

- The screens' last details, while building ROADMAP item 55.
