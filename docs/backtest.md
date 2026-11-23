# Backtest

How STL's own backtest works: what it tests, against what, with which money, costs and stocks, and which data. Planned with Ernest from 2026-11-19 to 2026-11-23. The screens are drafted below (*Screens, draft*), for another round with Ernest.

Today's template figures come from the POC's research ([backtest-research-findings.md](backtest-research-findings.md)). This backtest replaces them with STL's own engine, so the tested results describe the rules the app runs.

## Principle: the same money, the same costs

A test is fair only if the strategy and every baseline get the same money, on the same schedule, with the same costs, and are measured with the same formulas. Everything below follows from that.

The backtest runs the app's own engine (`Engine/`): the same rules, signals at the close, each trade at the next trading day's open, in the strategy's currency at each day's exchange rate (the baselines too). A rule the app changes, the backtest changes with it, so a result always describes what the app would have done.

**Checked against the app:** a backtest of a running simulation, with its settings and list from its start day, must give the same signals and trades the app recorded. It is the simulator's main test.

## What is tested

| Parameter | Default | Notes |
| --- | --- | --- |
| Rules | The template's tested defaults, or a strategy's settings | Any Advanced settings values |
| Stocks | Draft A: one fixed draft of 60 stocks from the S&P 500 | Or the strategy's own list, or one stock (see *Stocks*) |
| Index | S&P 500 | *Bear markets* and *Index trend* only: the index fund they trade (S&P 500, Nasdaq-100, DAX, Nikkei 225…). How often a market fell 20% depends on it |
| Period | 12 years, in 4-year parts | Both are parameters. 4 years gives the strategy time to profit from its buys |
| Budget | €6,000 (60 steps, one per drafted stock) | A test setting. A strategy's test uses its own budget. One stock: max steps × step size, so the budget fits it |
| Step | €100, up to 5 per stock | As the strategy's budget |
| Monthly limit | €250 a month (the budget over 24 months) | A cap on new money spent; unspent limit carries over in full (see *Money*). Limit = budget means no limit |
| Fee | 0.1% a trade | No fixed part, so results don't depend on the budget's size, only on how many steps it holds |
| Spread | 0.1% a trade | Added to the buy price and taken off the sell price |

No currency fee. Taxes are not included.

## Periods

- The windows are calendar years, counted back from today: with the defaults, 2015–18, 2019–22 and 2023–26 (to the last close), plus **All**.
- Each window is an independent test that starts with fresh money, as *How we tested* says today.
- **Warm-up:** the rules read about 15 months of prices before a window starts (the SMA 200, the 52-week high and the 168-day low need them), but nothing is bought before the start. It is what a strategy started that day in the app would see. Without it, the first year of each window would have no signals.
- At a window's end, holdings are valued at the last close. Nothing is sold to close the test, for the strategy or the baselines.
- The latest window runs to the last close, so it is shorter than the others (2023–26 is under 4 years today); its label says so.
- With 12 years, 2008 and 2000–02 are out. The years parameter can reach back to them.

## Money

- The test account starts with the whole budget in cash. Every line, strategy and baselines, spends at most the monthly limit of new money in a calendar month.
- **Unspent limit carries over in full** (Ernest, 2026-11-23: the simplest): by the end of month *m*, at most *m* × the limit is spent in total. A quiet stretch saves room for a busy one, up to the budget. Baselines 1 and 3 spend the limit every month anyway.
- Sale proceeds are reinvested at once, outside the limit. Only *Index trend* sells: each month it buys with that month's limit plus any money left from sales.
- When several signals on the same day need more money than is left, they are paid in a seeded random order (the same seed gives the same result, with no alphabetical bias). The ones that can't be paid follow the app's current rule (below).

**A signal with no money** (accepted for now, to change later). Today, when the budget can't pay a step, the app makes no signal but the rules count the step as bought, as the POC's backtest does ("as if the step had been bought"). So a comeback's first step is then gone, and *Bear markets*' step counter moves on. The backtest follows that rule in v1. Ernest's rule for later, for the app and the backtest together: a signal with no money waits, and buys once money is there while its window is still open (*Bear markets*: its step stays due while the conditions hold). See `ROADMAP.md`.

## Baselines

Decided by Ernest on 2026-11-22. Every baseline pays the same fee and spread, never sells, and is valued at each window's last close. The *pool* is the stocks the test uses: the draft, the list or the stock.

| # | Baseline | What it does | The question it answers |
| --- | --- | --- | --- |
| 1 | **S&P 500, monthly** | Buys SPY with each month's limit until the budget is used. | Does the strategy beat the market? (The product rule's index fund.) |
| 2 | **Same days, same money, same list** | Each time the strategy buys a step, the same amount goes into the whole pool that day, split evenly. | Does the strategy choose the right stocks in the list? Same days and money, so only the choice of stocks differs. |
| 3 | **Same stocks, monthly** | Buys the whole pool with each month's limit, split evenly, until the budget is used. | Does the timing matter much? Read against baseline 2: both buy the whole pool, only the days differ. |

So the gaps read as: strategy vs 1, against the market; strategy vs 2, the choice of stocks; 2 vs 3, the timing.

- With the limit equal to the budget, baselines 1 and 3 buy everything on the first day.
- Baseline 2 needs a pool of more than one: for one stock, and for *Bear markets* and *Index trend* (one fund), it is the strategy itself, so it isn't shown. For these two, baseline 3 is the same fund, monthly.

## Results

Per window and for **All**, for the strategy and each baseline, all **money-weighted** (Ernest, 2026-11-22): each euro counts for as long as it was invested, so early buys weigh more.

- **A year, on average**, on the money invested.
- **Worst calendar year**, money-weighted too.
- **Budget invested over time:** "On average, [n]% of the budget was invested". For the strategy, and next to each baseline. The formula, over the window's trading days:

  `budget invested = (1 / N) × Σ min(1, invested on day t / budget)`

  where *N* is the number of trading days in the window and *invested on day t* is the money put into the holdings still held that day (their cost, not their value). The whole budget invested on the first day reads 100%; money invested on the last day adds almost nothing. Example: half the budget on the first day and the other half at the window's middle reads 0.5 × 100% + 0.5 × 50% = 75%. After a sale (*Index trend*) the money is idle until bought again; reinvested gains can't take a day above 100%.
- The number of steps.
- **A note that idle money isn't counted:** "Rates count only the money invested, for as long as it was invested; budget waiting in cash isn't counted." Roughly, the rate on the whole budget would be the rate shown times the share invested.

The product rules hold: every result sits next to the index fund and the worst year.

## The dataset and the draft

Two different things:

- **The dataset** is everything a test can draw from: every stock that was in the S&P 500 at some point in the years tested (with its dates in the index), their daily prices, and the index funds. One snapshot a month (see *Data*).
- **Draft A** is the stocks a test uses: one fixed draft, drawn from the dataset's S&P 500 members on each window's start date (the same seed every time). Tuning and comparing always use it unless the strategy's list or one stock is chosen, so a difference comes from the settings, not from the stocks. There is no other draft (Ernest, 2026-11-22: random draws dropped).
- **Its size: 60** (Ernest, 2026-11-23), the most stocks a strategy can follow: large enough that the strategies find candidates in it and one lucky or unlucky stock weighs little.

**How it is drawn** (Ernest, 2026-11-23):

1. **Eligible:** the S&P 500 members on the part's start date with prices from the warm-up on.
2. **By sector:** each of the 11 sectors (Yahoo's, as for added assets) gets seats in proportion to its share of the eligible members.
3. **By size, within each sector:** low, middle and high thirds by average daily traded value (price × volume over the 60 trading days before the start), a size measure that exists for any past date.
4. **Drawn** at random within each sector and size group with a fixed, published seed. Each part gets its own draft, from its own members, with the same seed.
5. **Checked, never re-drawn to look better:**
   - *Representative:* per part, the draft's *same stocks, monthly* rate is within about ±2 points a year of the same baseline on all eligible members.
   - *Candidates:* each stock template, at its defaults, finds enough signals in it to invest its budget over the part (the steps and the budget invested per template and part).
   A failed check is reported and decided with Ernest (a larger draft, for example); changing the seed until the numbers look right would be cherry-picking.

## Stocks

Three ways to choose the pool:

1. **Draft A** (the default, for comparing): drawn from the S&P 500 as it stood on each window's start date (the stocks SPY held then), stratified by sector, with a fixed, published seed. The same draft and data make results of different strategies and settings comparable.
2. **The strategy's own list.** For shorter periods (default: the latest window only), since a list chosen today knows how its stocks did, which flatters the past. Shown next to the draft's result for the same window and settings. A stock without enough history at a window's start sits that window out, and the result says so.
3. **One stock.** The budget is sized for it (max steps × step size).

Not for *Your AI's top picks*, which has no rules to replay.

A stock may only be drawn if it was in the index on the window's start date and has prices from the warm-up on. Drawing from today's members would only pick the survivors.

## Data

- **Index membership:** the S&P 500 on any date, from [fja05680/sp500](https://github.com/fja05680/sp500) (MIT licence, since 1996, updated every couple of months), checked against a second point-in-time list where both exist. Renamed tickers (FB → META) need a symbol map.
- **Prices:** Yahoo's daily prices, adjusted for splits and dividends, as the app uses today. Dividends therefore count as reinvested in the same stock, before tax. *Pending:* the 15% US withholding tax on dividends isn't deducted.
- **Survivorship:** Yahoo keeps no history for delisted stocks, so former members that were delisted can't be drawn. Each window's report counts them ("31 of 503 members had no prices"), so the bias shows. Only a paid data source with delisted stocks would remove it.
- **A snapshot a month**, not daily: one compressed file per ticker plus a manifest (membership, symbol map, missing tickers), in the backend's object storage bucket, not in the `prices` table. Each result names the snapshot it used. Yahoo re-adjusts past prices after dividends, so a test re-run on a newer snapshot can move slightly; accepted (Ernest, 2026-11-20).
- **A quality report per snapshot**, on an owner page: members and how many have prices at each window's start, gaps over 5 trading days, one-day moves over 40% (usually a split error), renamed and missing tickers. The first report is reviewed before any result counts.

## Screens (draft)

Drafted in the planning chat (2026-11-20); Ernest will give them another round. All screens are owner-only. The numbers below are made up.

**Results**: periods as tabs (`2015-18 · 2019-22 · 2023-26 · All`); in each tab the strategy's figures (a year on average, worst year, steps), the "On average, [n]% of the budget was invested" line with the note that idle money isn't counted, a value chart, and the three baselines, all shown at once (Ernest, 2026-11-23: not hidden).

**Tuning (Advanced settings)**: a **Rerun tests** button instead of a rerun on every change. Columns *Default* (the template's tested defaults), *Before* (the strategy's saved settings, only when they differ from the defaults; otherwise the table is *Default* vs *Now*) and *Now* (the values on screen), all on Draft A with the same data, so a difference comes from the settings alone:

```
Backtest                         [ Rerun tests ]
Draft A · 12 years in 4-year parts · data 2026-11
[2015-18][2019-22][2023-26][ All ]

                       Default   Before    Now
A year, on average      +10.8%   +11.0%  +11.6%
Worst year               -14%     -15%    -17%
Steps                     41       45      52

Budget invested           48%      51%     58%
Rates count only the money invested; idle budget isn't counted.

Baselines
  1 S&P 500, monthly          +9.1%   (the same for every column)
  2 Same days, same list      +9.9%   +10.1%  +10.4%
  3 Same stocks, monthly      +9.8%   (the same for every column)
```

Baselines 1 and 3 depend on the stocks, periods, money and costs, not on the rules, so they show once. Baseline 2 follows each column's buy days.

**Other screens**: the setup (stocks, index, period, money, costs, the data snapshot, *Run backtest*) and the data report (owner page: members and coverage per window start, missing and renamed tickers, gaps, jumps).

## Biases

| Bias | How the backtest handles it |
| --- | --- |
| Look-ahead in prices | Signals at the close, trades at the next open; the warm-up reads only past prices |
| Look-ahead in the stocks | The S&P 500 as it stood at each window's start; a warning on the own list |
| Survivorship | Counted and shown per window; removed only by a paid data source |
| Tuning to the past | Every window is shown, not only All |
| Luck of the draw | One large draft, stratified by sector; the strategy's own list as a second view |
| Unequal money or costs | The same money, limit, costs and formulas for every line |
| Backtest ≠ app | The same engine; simulations use the same costs; the monthly limit becomes a strategy setting |

## In the app (decided, not built)

- **The monthly limit becomes a strategy setting:** "Spend at most [€x] a month", part of the budget, with unspent limit carried over in full as in the backtest. Existing strategies get x = the budget, so nothing changes for them, and it can be lowered afterwards (with *Change the budget*).
- **Simulations use the backtest's costs:** fee 0.1% + spread 0.1% a trade, no fixed part, for new trades from the change on (past trades keep the fee they were recorded with). Manual strategies keep the fee the user enters (`ROADMAP.md` item 41).

## Open points

- **All:** one continuous 12-year test, or the parts combined (proposed: combined, the average of the parts' rates and the worst year of any part).
- Whether *Saved tests* (keeping results to compare strategies side by side) is in v1 (proposed: later).
- Where the backtest lives: Advanced settings, the strategy page's Backtest tile, *How we tested*.
- The screens, once the functionality is settled.
- The order of the PRs (proposed in `ROADMAP.md`).
