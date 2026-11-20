# Backtest

How STL's own backtest works: what it tests, against what, with which money, costs and stocks, and which data. Planned with Ernest on 2026-11-19 and 2026-11-20. The screens are drafted below (*Screens, draft*), for another round with Ernest.

Today's template figures come from the POC's research ([backtest-research-findings.md](backtest-research-findings.md)). This backtest replaces them with STL's own engine, so the tested results describe the rules the app runs.

## Principle: the same money, the same costs

A test is fair only if the strategy and every baseline get the same money, on the same schedule, with the same costs, and are measured with the same formulas. Everything below follows from that.

The backtest runs the app's own engine (`Engine/`): the same rules, signals at the close, each trade at the next trading day's open, in the strategy's currency at each day's exchange rate (the baselines too). A rule the app changes, the backtest changes with it, so a result always describes what the app would have done.

**Checked against the app:** a backtest of a running simulation, with its settings and list from its start day, must give the same signals and trades the app recorded. It is the simulator's main test.

## What is tested

| Parameter | Default | Notes |
| --- | --- | --- |
| Rules | The template's tested defaults, or a strategy's settings | Any Advanced settings values |
| Stocks | A draft of 30 from the S&P 500 | Or random draws, the strategy's own list, or one stock (see *Stocks*) |
| Index | S&P 500 | *Bear markets* and *Index trend* only: the index fund they trade (S&P 500, Nasdaq-100, DAX, Nikkei 225…). How often a market fell 20% depends on it |
| Period | 12 years, in 4-year parts | Both are parameters. 4 years gives the strategy time to profit from its buys |
| Budget | €3,000 | One stock: max steps × step size, so the budget fits it |
| Step | €100, up to 5 per stock | As the strategy's budget |
| Monthly limit | `[€x a month]` (open) | At most this much new money is spent in a calendar month. Limit = budget means no limit |
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
- **No carry-over in v1:** money not spent in a month doesn't raise the next month's limit. Whether unspent money should carry over (for example up to 3 months) stays open, to be settled by running the backtest with and without it.
- Sale proceeds are reinvested at once, outside the limit. Only *Index trend* sells: each month it buys with that month's limit plus any money left from sales.
- When several signals on the same day need more money than is left, they are paid in a seeded random order (the same seed gives the same result, with no alphabetical bias). The ones that can't be paid follow the app's current rule (below).

**A signal with no money** (accepted for now, to change later). Today, when the budget can't pay a step, the app makes no signal but the rules count the step as bought, as the POC's backtest does ("as if the step had been bought"). So a comeback's first step is then gone, and *Bear markets*' step counter moves on. The backtest follows that rule in v1. Ernest's rule for later, for the app and the backtest together: a signal with no money waits, and buys once money is there while its window is still open (*Bear markets*: its step stays due while the conditions hold). See `ROADMAP.md`.

## Baselines

Every baseline gets the same money, under the same monthly limit, pays the same fee and spread, never sells, and is valued at each window's last close.

| Baseline | What it does | The question it answers |
| --- | --- | --- |
| **Same stocks, monthly** (the headline comparison) | Buys the whole pool of stocks with each month's limit, split evenly across them, until the budget is used. The pool is the draft, the list or the stock; for *Bear markets* and *Index trend*, the same index fund. | Did the rules' choices, which stocks from the pool and when to buy them, beat buying the whole pool steadily? |
| **S&P 500, monthly** | Buys SPY with each month's limit until the budget is used. | Did the whole plan beat steady buying of the index? (The product rule's index fund.) |
| **S&P 500, same days** | Each step the strategy buys, the same amount goes into SPY that day. | Did the money the strategy invested beat the index? (Today's "alpha".) |

With the limit equal to the budget, the two monthly baselines buy everything on the first day.

## Results

Per window and for **All**, for the strategy and each baseline:

- **A year, on average.** A switch on the results chooses how:
  - **On money invested** (the default, since a strategy invests over time by design): a money-weighted yearly rate, where each euro counts for as long as it was invested, so early buys weigh more.
  - **On the whole budget:** the yearly rate of the whole account, cash included at 0%.
  - One line under the switch explains the gap: "On average, [n]% of the budget was invested" (the share invested each day, averaged over the window). Roughly, the rate on the whole budget is the rate on money invested times that share.
- **Worst calendar year**, measured the same way as the switch.
- **Alpha:** the strategy's rate against *S&P 500, same days*.
- The number of steps.

The product rules hold: every result sits next to the index fund and the worst year.

## The dataset and the draft

Two different things:

- **The dataset** is everything a test can draw from: every stock that was in the S&P 500 at some point in the years tested (with its dates in the index), their daily prices, and the index funds. One snapshot a month (see *Data*).
- **A draft** is the 30 stocks one test uses, drawn from the dataset's S&P 500 members on each window's start date. **Draft A** is the fixed one: tuning and comparing always use it unless another pool is chosen, so a difference comes from the settings, not from the stocks.

## Stocks

Four ways to choose the pool:

1. **A draft** (the default, for comparing). 30 stocks drawn from the S&P 500 as it stood on each window's start date (the stocks SPY held then), stratified by sector, with a published seed. The same draft (same seed, same data) makes results of different strategies and settings comparable.
2. **Random draws.** The same method, 25 times with new seeds. Each draw runs the whole test, strategy and baselines. Shows the median, the 10th–90th percentile range, and how often the strategy beat *Same stocks, monthly* ("in 19 of 25 draws"). It says whether a result came from the luck of the stocks drawn. Draft mode only.
3. **The strategy's own list.** For shorter periods (default: the latest window only), since a list chosen today knows how its stocks did, which flatters the past. Shown next to the draft's result for the same window and settings. A stock without enough history at a window's start sits that window out, and the result says so.
4. **One stock.** The budget is sized for it (max steps × step size).

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

**Results**: periods as tabs (`2015-18 · 2019-22 · 2023-26 · All`); in each tab the strategy's figures (a year on average, worst year, steps), the money-invested / whole-budget switch with its "On average, [n]% of the budget was invested" line, and a value chart. The baselines open with **Show baselines** in the same tab, so the main view stays the backtest alone.

**Tuning (Advanced settings)**: a **Rerun tests** button instead of a rerun on every change. Columns *Default* (the template's tested defaults), *Before* (the saved settings, only when they differ from the defaults) and *Now* (the values on screen), all on Draft A with the same data, so a difference comes from the settings alone:

```
Backtest                         [ Rerun tests ]
Draft A · 12 years in 4-year parts · data 2026-11
[2015-18][2019-22][2023-26][ All ]

                       Default   Before    Now
A year, on average      +10.8%   +11.0%  +11.6%
Worst year               -14%     -15%    -17%
Steps                     41       45      52

▸ Show baselines
  Same stocks, monthly   +9.8%   (the same for every column)
  S&P 500, monthly       +9.1%   (the same for every column)
  Alpha, same days       +1.6     +1.5    +1.4
```

*Same stocks, monthly* and *S&P 500, monthly* depend on the stocks, periods, money and costs, not on the rules, so they show once. Only the alpha follows each column's buy days.

**Other screens**: the setup (stocks, index, period, money, costs, the data snapshot, *Run backtest*); the random draws (median, 10th–90th percentile range, "beat Same stocks, monthly in [n] of 25 draws"); and the data report (owner page: members and coverage per window start, missing and renamed tickers, gaps, jumps).

## Biases

| Bias | How the backtest handles it |
| --- | --- |
| Look-ahead in prices | Signals at the close, trades at the next open; the warm-up reads only past prices |
| Look-ahead in the stocks | The S&P 500 as it stood at each window's start; a warning on the own list |
| Survivorship | Counted and shown per window; removed only by a paid data source |
| Tuning to the past | Every window is shown, not only All |
| Luck of the draw | Random draws |
| Unequal money or costs | The same money, limit, costs and formulas for every line |
| Backtest ≠ app | The same engine; simulations use the same costs; the monthly limit becomes a strategy setting |

## In the app (decided, not built)

- **The monthly limit becomes a strategy setting:** "Spend at most [€x] a month", part of the budget. Existing strategies get x = the budget, so nothing changes for them, and it can be lowered afterwards (with *Change the budget*).
- **Simulations use the backtest's costs:** fee 0.1% + spread 0.1% a trade, no fixed part, for new trades from the change on (past trades keep the fee they were recorded with). Manual strategies keep the fee the user enters (`ROADMAP.md` item 41).

## Open points

- The default monthly limit for a template's test (proposed: the budget over 24 months, so the money has time to work within a 4-year window).
- Carry-over of unspent money (no carry-over in v1).
- The product rule "results always next to the index fund": the results' main view shows the backtest alone, with the baselines behind *Show baselines*. Keep one index line in the main view, or make the backtest an exception to the rule?
- *Before* in the tuning table: the saved settings is assumed; in the new-strategy flow (nothing saved yet), the previous run's.
- A second round on the screens; whether *Saved tests* (comparing strategies side by side) is in v1.
- How the backtest fits into the strategy page's Backtest tile and *How we tested*.
- The order of the PRs (proposed in `ROADMAP.md`).
