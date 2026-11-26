# Backtest

How STL's own backtest works: what it tests, against what, with which money, costs and stocks, and which data. Planned with Ernest from 2026-11-19 to 2026-11-24. It uses the app's words since #281: *amount per trade* and *trades* (*step* in the code). The screens are drafted below; their last details are settled while building them (ROADMAP item 55).

Today's template figures come from the POC's research ([backtest-research-findings.md](backtest-research-findings.md)). This backtest replaces them with STL's own engine, so the tested results describe the rules the app runs.

## Principle: the same money, the same costs

A test is fair only if the strategy and every baseline get the same money, on the same schedule, with the same costs, and are measured with the same formulas.

The backtest runs the app's own engine (`Engine/`): the same rules, signals at the close, each trade at the next trading day's open, in the strategy's currency at each day's exchange rate (the baselines too). A rule the app changes, the backtest changes with it, so a result always describes what the app would have done.

**Checked against the app:** a backtest of a running simulation, with its settings and list from its start day, must give the same signals and trades the app recorded. It is the simulator's main test.

## What is tested

| Parameter | Default | Notes |
| --- | --- | --- |
| Rules | The template's tested defaults, or a strategy's settings | Any Advanced settings values |
| Stocks | The test draft: 60 stocks from the S&P 500, rolled at each period's start | Or the strategy's own list, or one stock (see *Stocks*) |
| Index | S&P 500 | *Bear markets* and *Index trend* only: the index fund they trade (S&P 500, Nasdaq-100, DAX, Nikkei 225…). How often a market fell 20% depends on it |
| Years | 16 years, in 4-year periods (Ernest, 2026-11-23) | Both are parameters. 4 years gives the strategy time to profit from its buys. One stock and the strategy's own list: the latest period only |
| Budget | €6,000 (60 trades, one per drafted stock) | A test setting. A strategy's test uses its own budget. One stock: max trades × amount per trade, so the budget fits it |
| Amount per trade | The template's: €100, up to 5 trades a stock (*The comeback*: €200, up to 2) | As the strategy's budget |
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
- With 16 years, 2008 and 2000–02 are out (2011 brings a fall of almost 20%). On the test draft the years can't go past the dataset's; reaching back further means drawing a longer draft, where more former members have no prices. The strategy's list, one stock and an index can go back as far as Yahoo has their prices.

## Money

- The test account starts with the whole budget in cash. Every line, strategy and baselines, spends at most the monthly limit of new money in a calendar month.
- **Unspent limit carries over in full** (Ernest, 2026-11-23: the simplest): by the end of month *m*, at most *m* × the limit is spent in total. A quiet stretch saves room for a busy one, up to the budget. Baselines 1 and 3 spend the limit every month anyway.
- Sale proceeds are reinvested at once, outside the limit. Only *Index trend* sells: each month it buys with that month's limit plus any money left from sales.
- When several signals on the same day need more money than is left, they are paid in a seeded random order (the same seed gives the same result, with no alphabetical bias). The ones that can't be paid follow the app's current rule (below).

**A signal with no money** (accepted for now, to change later). Today, when the budget can't pay a trade, the app makes no signal but a Simulation's rules count the trade as bought, as the POC's backtest does (a Manual strategy holds only the trades the user did, since #282). So a comeback's first trade is then gone, and *Bear markets*' trade counter moves on. The backtest follows that rule in v1. Ernest's rule for later, for the app and the backtest together: a signal with no money waits, and buys once money is there while its window is still open (*Bear markets*: its trade stays due while the conditions hold). See `ROADMAP.md`.

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

- **The dataset** holds the test draft's stocks (about 90–100 over the 4 periods, since the draft rolls), SPY (baseline 1 and the default index) and the templates' starter lists (their stocks and funds), with their daily prices. One snapshot a month (see *Data*). Anything else (an edited list, another stock, another index) is fetched when a test runs.
- **Drawing the draft** needs every stock that was in the S&P 500 in the years tested (with its dates in the index): their prices give the size thirds and the representativeness check. All of them are downloaded only when the draft is drawn (Ernest, 2026-11-24): the first time, when a new period starts (2027 adds 2027–30), when a membership correction touches a drawn period, or when the draft's size or method changes. The checks run again each time. Then only the draft's stocks are kept.
- **The test draft** is the stocks a test uses: one draft, drawn at the first period's start and rolled at each later one (below), with a fixed seed. Tuning and comparing always use it unless the strategy's list or one stock is chosen, so a difference comes from the settings, not from the stocks. There is no other draft (Ernest, 2026-11-22: random draws dropped).
- **Its size: 60** (Ernest, 2026-11-23), the most stocks a strategy can follow: large enough that the strategies find candidates in it and one lucky or unlucky stock weighs little.

**How it is drawn** (Ernest, 2026-11-23):

1. **Eligible:** the S&P 500 members on the first period's start date (2011) with prices from the warm-up on.
2. **By sector:** each of the 11 sectors (Yahoo's, as for added assets) gets seats in proportion to its share of the eligible members.
3. **By size, within each sector:** low, middle and high thirds by average daily traded value (price × volume over the 60 trading days before the start), a size measure that exists for any past date.
4. **Drawn** at random within each sector and size group with a fixed, published seed: the draft for the first period.
5. **Rolled** (Ernest, 2026-11-24): at each later period's start, the drafted stocks still in the index stay; each one that left is replaced by a member drawn with the same seed from the same sector and size third, measured on that date. Most stocks stay the same across periods, as in an index fund, with no look-ahead: a stock that leaves the index during a period stays until the period ends. A new period (2027–30) rolls from the last one. Sector shares can drift as the index changes; the representativeness check catches it.
6. **Checked, never re-drawn to look better:**
   - *Representative:* per period, the draft's *same stocks, monthly* rate is within about ±2 points a year of the same baseline on all eligible members.
   - *Candidates:* each stock template, at its defaults, has 4–5 different candidates in it every calendar year (Ernest, 2026-11-23). The report shows the count per template and year, with the trades and the budget invested.
   A failed check is reported and decided with Ernest (a larger draft, for example); changing the seed until the numbers look right would be cherry-picking.

## Stocks

Three ways to choose the pool:

1. **The test draft** (the default, for comparing): 60 stocks from the S&P 500 as it stood at the first period's start (the stocks SPY held then), by sector and size, with a fixed seed, rolled at each later period's start (*How it is drawn*). The same draft and data make results of different strategies and settings comparable.
2. **The strategy's own list.** Over less time (default: the latest period only), since a list chosen today knows how its stocks did, which flatters the past. Not limited to the dataset's years. A stock without enough history at a period's start sits that period out, and the result says so.
3. **One stock.** The budget is sized for it (max trades × amount per trade). The latest period only, by default, as for the own list: the stock is chosen knowing how it did.

Not for *Your AI's top picks*, which has no rules to replay.

A stock may only be drawn if it was in the index on the period's start date and has prices from the warm-up on. Drawing from today's members would only pick the survivors.

## Data

- **Index membership:** the S&P 500 on any date, from [fja05680/sp500](https://github.com/fja05680/sp500) (MIT licence, since 1996, updated every couple of months), checked against a second point-in-time list where both exist. Renamed tickers (FB → META) need a symbol map.
- **Prices:** Yahoo's daily prices, adjusted for splits and dividends, as the app uses today, so dividends count as reinvested in the same stock, before tax. *Pending:* the 15% US withholding tax on dividends isn't deducted.
- **Ready or fetched** (Ernest, 2026-11-24): the dataset holds the test draft's stocks, SPY and the templates' starter lists. A test on any other stock or index (an edited list, another single stock, another index for *Bear markets* or *Index trend*) fetches its prices from Yahoo when it runs, kept for reuse. A list, one stock or an index isn't limited to the dataset's years (the missing years are fetched); the test draft is.
- **Survivorship:** Yahoo keeps no history for delisted stocks, so former members that were delisted can't be drawn. The report made when the draft is drawn counts them, per period ("31 of 503 members had no prices"), so the bias shows. Only a paid data source with delisted stocks would remove it.
- **A snapshot a month**, not daily, of the dataset (the draft's stocks, SPY, the starter lists): one compressed file per ticker plus a manifest (membership, symbol map, missing tickers), in the backend's object storage bucket, not in the `prices` table. Each result names the snapshot it used. Yahoo re-adjusts past prices after dividends, so a test re-run on a newer snapshot can move slightly; accepted (Ernest, 2026-11-20).
- **A quality report**, on an owner page: per snapshot, the draft's stocks and SPY; when the draft is drawn, all members and how many have prices at each period's start, gaps over 5 trading days, one-day moves over 40% (usually a split error), renamed and missing tickers. The first report is reviewed before any result counts.

## The data as built (item 52, #283)

What the first version does, with the assumptions made while building it (listed in #283). The owner page is `/admin/backtest-data` (screen E below).

- **Membership, committed.** fja05680/sp500's history is kept in the repo as ticker intervals (`backend/src/StrategyTradingLab.Api/Backtest/Data/`, rebuilt by `backend/scripts/backtest-membership.py`, which checks the intervals give back every fja row). A correction arrives as a PR; each draw records the files' hash, and the page says "draw again" when they changed.
- **The second list is Wikipedia's**: its current list walked back through its changes table, compared with fja at each period start. The differences are listed per period; most are renames (Wikipedia records none). Its names are also what Yahoo's name for a symbol is checked against.
- **The symbol map is dated and hand-made** (`symbol-map.csv`): fja writes a ticker as it was then, Yahoo keeps a company under today's ticker. A row applies before a date (2011's `IR` is today's `TT`; today's `IR` is another company) and may say the ticker is another company's today (`S`), so it is never asked of Yahoo. Without a row, a ticker whose Yahoo name doesn't match its Wikipedia name (one starts with the other's first word, or its first 3 letters when that word is shorter) is left out (*Name differs*), so a reused ticker never brings another company's prices. A ticker Wikipedia or Yahoo doesn't name gets no name check; the report lists those (*Names not checked*) for a look. Two tickers for one Yahoo symbol on a day count once. A stock is known by its Yahoo symbol, so `FB` in 2015 and `META` in 2023 are one stock when the draft rolls. Two share classes of one company (GOOG and GOOGL, FOX and FOXA: the same Wikipedia name without its class) count once: the one most traded the first time the company is drawable stays its class in later periods (so a drafted class keeps its seat), and the other is listed as *Other share class drawn*.
- **Eligible on a period's start:** a member that day with a Yahoo symbol, prices on Yahoo, a matching name, a sector, prices from within 7 days of the warm-up's start, and at least 40 traded days in the 60 trading days before the start. Each member that isn't is listed with its reason.
- **Size:** split-adjusted (not dividend-adjusted) close × volume, the money traded, averaged over the 60 US trading days (SPY's) before the start. **Sector:** Yahoo's today, not the one at the time; for a listing Yahoo's search no longer finds while its prices are still there (delisted or renamed lately), the hand-made `sectors.csv` in Yahoo's sector names.
- **The draw:** seats by largest remainder; within a sector an even split across the thirds, the remainder to thirds in a seeded order. The seed is **20261125**, and the generator (SplitMix64, one stream per sector and third) is fixed by its definition, so the same membership and prices give the same draft on any machine. A roll replaces a stock from its seat's sector and third, measured that day; with none left there, from the nearest third (the report says so). A drafted stock still in the index that can't be drawn (its prices stopped) gives up its seat too, since it can't be bought that period; the report marks it *still in the index*. A kept stock's size third is re-measured each period, and a later replacement draws from that third. A seat no member can fill stays open and is tried again at the next period's start.
- **Representative check:** *same stocks, monthly* (baseline 3) on the draft and on every eligible member, money-weighted, in dollars: both pools buy on the same days, so the euro moves them alike (the simulator, item 53, runs it in the strategy's currency). Fee and spread as in *What is tested*; holdings valued at the last close, nothing sold.
- **Candidates check:** the app's engine runs each stock template (*Winners at a discount*, *The comeback*) at the defaults in use (`/admin/templates`) over each period's draft, from the warm-up. Per calendar year, each year from scratch (positions reset on 1 January, Ernest 2026-11-25, so stocks bought in earlier years count again; a buy window already open on the year's last trading day, judged on that day alone, isn't counted again as the next year's first signal, except in a period's first year, whose day-one buys are the period's): the different stocks with a buy signal (the check: at least 4), the stocks that were candidates on some day (meeting the candidate rules, not only held), and the buy signals, each stock buying up to its max trades with no budget. Trades and the budget invested per template come with the templates' figures on the draft (item 56), which run the simulator over it.
- **Running it:** the owner starts a draw on the page; it runs in the background (one Yahoo request at a time, about 1,500, roughly 15 minutes), then takes the month's snapshot. *Take this month's snapshot* fetches the draft's stocks, SPY and the starter lists again, from the draw's own first warm-up. Only Yahoo's answer for a symbol (404, 400 or 422, its own "no data") makes a member missing: a refusal (401, 403, 429), a timeout (408 or none), a server error or no connection still failing after two retries stops the run, and so does a period where under 60% of the members can be drawn; the stored draw and snapshot stay as they were, since a smaller pool would change the draft. Each run writes its files to a folder of its own (`snapshots/yyyy-MM/{run}/`), and `snapshot.json` points at it only once the run has finished. The page says "draw again" when the membership files changed or a new year moved the periods.
- **Storage** (the branch's bucket, `backtest/`): `draw.json` (the latest draw's report, with a dated copy under `draws/`), and per month `snapshots/yyyy-MM/{run}/` with `manifest.json` (the draft per period, the membership's hash and sources, the renamed and missing tickers, the files) and `prices/{symbol}.csv.gz` (`date,open,high,low,close,volume`, adjusted). `snapshot.json` is the latest snapshot's report. A snapshot taken again in the same month becomes that month's snapshot.
- **Price warnings:** gaps over 5 US trading days, one-day closes moving over 40%, and prices that stop more than 5 trading days before the last day, for every ticker of the snapshot; and the one-day moves over 40% of the other drawable members, which count in the representative check (a split or spin-off Yahoo didn't adjust, as Aimco's in 2020, would move its rate).

### The first report (2026-11-25), and what was decided

The first draw on production (seed 20261125, 709 symbols asked):

| Period | Members | Can be drawn | Can't | Representative (draft vs all) | Kept, replaced |
| --- | --- | --- | --- | --- | --- |
| 2011–14 | 497 | 336 | 161 | +19.2% vs +19.4% | (drawn) |
| 2015–18 | 499 | 372 | 127 | +10.0% vs +7.9% | 59, 1 |
| 2019–22 | 505 | 436 | 69 | +10.6% vs +10.1% | 58, 2 |
| 2023–26 | 503 | 468 | 35 | +16.2% vs +13.8% | 57, 3 |

- **Survivorship is larger than expected:** 147 members have no prices on Yahoo (delisted since), 115 of them in 2011–14, not about 31. So the early periods are drawn from companies that survived, and their results lean high. Only a paid source with delisted stocks would fix it.
- **The roll replaces few stocks** (1 to 3 a period, not the 8 to 10 the index's turnover suggests), and the draft holds 66 stocks over the four periods, not 90 to 100: drawn from survivors, most drafted stocks stayed in the index. **Kept as is** (Ernest, 2026-11-25): rolling, like an index fund.
- **The representative check misses by a little in 2 periods** (2.1 and 2.4 points). **Accepted** (Ernest, 2026-11-25): re-drawing with another seed would be cherry-picking, and a larger draft goes past the 60 stocks a strategy follows.
- **The candidates check** counted positions across the whole period, so the last years read low once stocks had taken their trades; it now runs each year from scratch (above).
- **Fixed from the report** (#284): 25 *Name differs* were 15 reused tickers (now map rows saying so) and 10 of the same company under a name the check couldn't match (IBM, LLY, SLB, PCG, WAB…, now map rows), plus VIAV (JDSU) unchecked (the page lists every map row used, as *Symbol map rows used*); 10 of the 11 *No sector* were listings Yahoo's search dropped (EA, CMA, AVB, EQR…), now in `sectors.csv`; the 11th, BBBY, is the former Overstock today (it took the name and ticker in 2025), now a map row that keeps it out. A map row vouches for a name only before a spin-off Yahoo doesn't adjust: AIV (Aimco, December 2020) is left out from 2019, CNX (coal, November 2017) from 2015. The 4 price warnings are real moves (GL −53% in April 2024, LUMN +93% in August 2024, FMC −47%, NWL +41%), not split errors.

### The second report (2026-11-25), accepted

After #284's fixes (Ernest accepted it, #285):

| Period | Can be drawn | Can't | Representative (draft vs all) |
| --- | --- | --- | --- |
| 2011–14 | 345 | 152 | +17.2% vs +19.1% |
| 2015–18 | 379 | 120 | +10.0% vs +7.8% (2.2 points, accepted as before) |
| 2019–22 | 440 | 65 | +10.7% vs +10.2% |
| 2023–26 | 472 | 31 | +14.5% vs +14.0% |

- No member is left out for its name or its sector any more; what remains is delisted companies, tickers that are another company today, and prices that start too late. The larger pools moved about 17 of the 60 seats; 68 stocks over the four periods.
- **Candidates:** 31 of 32 template-years pass. *The comeback* in 2013 (2 stocks, a strong bull year) is **accepted** (Ernest, 2026-11-25).
- **Price moves:** all real (GameStop's 2021 squeeze, March 2020, Moderna +177% on 19 August 2026 on its melanoma vaccine data) except **Danaher, +61% on 5 July 2016**: Yahoo recorded the Fortive spin-off as a split. It lifts 2015–18's all-members rate by about 0.1 point; the fix (DHR left out of 2015–18) waits in `TODO.md`.

## The simulator as built (item 53)

`Backtest/BacktestSimulator.cs`, pure code over the engine: prices in, results out. Loading the snapshot and running a test on request is item 54. Assumptions made while building it (listed in its PR):

- **Each stock's rules run on their own**, from the period's first day with the warm-up before it (`RulesRunner`, as a simulation started that day), and act on every signal, paid or not: today's rule for a signal with no money.
- **Money is decided on the signal's day** (when the app makes the signal) and the trade made at the stock's next open. The monthly limit counts the signal day's month: by the end of month *m* of the period, at most *m* × the limit of new money, and never more than the budget. A trade that doesn't fit is not made, and no part of it (the app makes no signal); the result counts these as *unpaid*. A signal on the period's last trading day trades after the period: nothing is bought.
- **The seeded order:** a day's signals are paid in an order shuffled with the draft's seed (20261125) and the date (`DraftDraw.Shuffle`, SplitMix64), so the same data gives the same result and no symbol is favoured.
- ***Index trend* under a monthly limit:** a buy spends the room the limit leaves plus any sale proceeds (reinvested outside the limit); while it holds the fund, it takes each new month's room at its first open of the month (a *top-up*, a trade with no number), so a limit below the budget still puts the budget to work. With limit = budget (every existing strategy) there is never room for a top-up, so it buys all in and sells all out as the app does. Item 57 makes the app do the same.
- **Worst year:** each calendar year's own money-weighted return, solved on a clock that runs only while the line holds something (holdings carried in count as put in on 1 January at their value): money put in during December counts for its few days, and a fund sold in February and not bought again had its year from January to February. No yearly rate is involved, so a short year's big move is never out of range.
- **Budget at work** averages over the index fund's (SPY's) trading days in the period. **Baselines 1 and 3** buy on SPY's first trading day of each month; a stock that doesn't trade that day (another exchange's holiday) buys at its next open in the month. **Baseline 2** splits each of the strategy's buys over the pool's stocks that trade that day; it is left out for a pool of one.
- **Currency:** every amount is in the test's currency; a trade converts at its day's rate, holdings are valued at each close's rate.
- **Checked** on a hand-worked quarter (the limit, carry-over, unpaid signals, the rate, the worst year, the budget at work, the three baselines), on the POC's golden runs of all four templates (with money for every signal, the trades are the POC's signals at the next open), and against the app: a simulation checked each evening by the daily check, over 400 days and three stocks, records the same trades (day, stock, trade number) as the backtest of it from its start.
- **Found on the way:** the daily check pays a run's new signals stock by stock, so a run that catches up several days at once (the server down for a few days) spends the budget in the stocks' order, not by date. A day-by-day run is chronological, as the backtest. Noted in `ROADMAP.md` (*Ideas*).

## Running a test (item 54)

`POST /admin/backtest` (owner only, `Backtest/BacktestRunner.cs`) runs one test in the request and answers with the setup it used (every default filled in), the snapshot's month, each period's pool (and the stocks that sat it out) and the simulator's results. Assumptions made while building it (listed in its PR):

- **Defaults:** the template's defaults in use (`/admin/templates`), with any settings given over them, checked as a strategy's are (known, in range, on a step); the test draft over 16 years in 4-year periods; €6,000 at most €400 a month, with the template's amount per trade and trades a stock (€100 × 5; *The comeback* €200 × 2); fee and spread 0.1%; in euro (USD or GBP on request). A list or one stock: the latest period only. One stock: budget = max trades × amount per trade.
- ***Bear markets* and *Index trend*** trade one fund (SPY unless another is given) with **the template's own budget and no monthly limit** unless the request sets them: €3,000 in 5 trades of €600, and €3,000 all in. That is how their figures were tested so far (all in against buying the index on day one); the €400 limit would leave *Bear markets*' €600 trade waiting a month and turn *Index trend* into monthly buying. To settle with item 56's numbers.
- **The test draft per period:** each tested period uses the draft of the drawn period it starts in, so other period lengths still read the drawn drafts; a period before the draft is refused, and one after it asks for a new draw.
- **Prices:** a symbol in the snapshot is read from it whenever the test starts no earlier than the snapshot's own first warm-up (a parsed file stays in memory: a snapshot never changes); any other (an edited list, another stock, another index) is fetched from Yahoo from the first warm-up on and kept for the month under `backtest/fetched/yyyy-MM/` (its prices and a small description with its currency); next month's tests fetch it again. In the request there are no retries: Yahoo refusing or timing out says to try again, apart from a symbol it has no prices for.
- **Exchange rates** come from Yahoo's `EUR{X}=X` closes, fetched and kept the same way: the app's stored rates only start two years before the earliest strategy. A test whose stocks are all in its own currency needs none.
- **Currencies:** a symbol known to the app has the asset's currency; the draft's stocks and SPY are in dollars; any other is asked of Yahoo.
- **Kept results:** a result is kept under `backtest/results/{key}.json`, the key a hash of the rules (params with their keys in order), the stocks per period, the periods, the money, the costs, the currency, the snapshot and the month. The same test again gives it back without running.
- **Errors:** a request out of range is a 400 with the reason; missing data (no snapshot, Yahoo has no prices, a period past the draft) a 409.

## Where it lives

Decided by Ernest on 2026-11-23. All owner-only.

- **The Backtest page**, its own page: the test's settings (stocks, index, years, money, costs) and the full results. It is where the *test* is tuned; the strategy's settings are tuned in Advanced settings.
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

- *Edit rules* opens Advanced settings for this strategy (or for the new strategy being set up).
- **Years and data:** with the test draft, the years can't reach past the dataset (16 years and their warm-up, shown on the *Data* line). The strategy's list, one stock and an index aren't limited to it: their prices come from Yahoo, as far back as Yahoo has them. A list and one stock default to the latest period.
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

52 trades · test draft: 60 stocks
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

**E · Backtest data** (owner page, `/admin/backtest-data`): the snapshot and *Refresh*; the test draft per period (stocks kept and replaced); from the last draw, the members and how many have prices at each period's start, missing and renamed tickers (lists), and the two checks (representative, candidates per template and year); warnings (gaps, jumps), each with ticker and date.

## The screens as built (item 55)

Assumptions made while building them (listed in its PR):

- **The Backtest page is `/backtest`, owner only, in English** like the owner pages; its entry links and the Advanced settings panel are translated (English and Catalan), as they sit on translated screens. The page opens preset from the URL (`template`, `strategy`, `stock`, and `from` for the top bar's way back) and runs the test at once.
- **A strategy's test** uses its settings, budget, amount per trade, trades a stock and currency, with **no monthly limit** (limit = budget) until strategies have one (item 57). *This strategy's list* is offered in *Edit*, over the latest period.
- **The settings on screen** (Advanced settings' *Open the backtest page*) are handed over in the page's state; the line under the title says where the rules come from and how many differ from the tested values.
- **The panel** runs on *Rerun tests*, not on open: up to three tests (Default, Saved, Current), each kept by its key, so a repeat is quick. *Saved* shows only when a running strategy's settings differ from the defaults, *Current* only when the screen's differ from both.
- **The chart** shows the gain on the money invested at each month's end, for the strategy and baseline 1, per period (not on *All*). Tapping a baseline to add its line is left for later.
- **Trades** on *All* are every period's.

## The templates' figures (item 56)

Run on production on 2026-11-25 (snapshot 2026-11, the defaults in use, in euro), through `POST /admin/backtest`; they replace the POC's in `TemplateCatalog` and *How we tested*. A year on average and the worst year, money-weighted; the index column is baseline 1 (*S&P 500, monthly*) for the stock templates, and the fund bought with the whole budget on day one (baseline 3 with no limit) for *Bear markets* and *Index trend*.

| Template | 2011–14 | 2015–18 | 2019–22 | 2023–26 | All | Worst year |
| --- | --- | --- | --- | --- | --- | --- |
| *Winners at a discount* | +18.9% (21.4) | +9.6% (7.3) | +9.2% (11.6) | +11.2% (17.4) | **+12.2%** (14.4) | −12.7% (−13.1), 2022 |
| *The comeback* (2 trades of €200, since 2026-11-26) | +24.3% (21.4) | +10.7% (7.3) | +14.4% (11.6) | +11.8% (17.3) | **+15.3%** (14.4) | −15.9% (−13.1), 2015 |
| *The comeback* before (5 trades of €100) | +23.3% (21.4) | +3.5% (7.3) | +13.4% (11.6) | +10.4% (17.4) | +12.7% (14.4) | −30.6% (−13.1), 2015 |
| *Bear markets* (SPY) | no trade (17.9) | no trade (8.5) | +16.5% (15.6) | no trade (18.2) | **+16.5%** (15.6) | −13.1% (−13.1), 2022 |
| *Index trend* (SPY) | +16.6% (17.9) | +5.8% (8.5) | +9.2% (15.6) | +15.9% (18.2) | **+11.9%** (15.0) | −18.6% (−13.1), 2022 |

The index's figure in brackets. *Index trend* on the other tabs (All, against the fund held): Nasdaq-100 +17.1% (19.2), worst −13.4% (−28.4); Developed ex-US +3.9% (7.8), worst −10.2% (−10.3); DAX (the index, ^GDAXI) +7.1% (8.3), worst −11.3% (−18.3); Nikkei 225 (the index, ^N225) +0.3% (8.2); Vanguard 500 +12.4% (14.9).

- **Budget at work:** about 73% (*Winners at a discount*) and 83% (*The comeback*) against 85% for the S&P 500, monthly; *Index trend* 80%; *Bear markets* 17% (it trades in one period of four).
- **The budget runs out early:** at €400 a month, both stock templates spend the €6,000 within about 15 months of each period's start (60 trades a period), so the later years only hold, and the signals after go unpaid (54 to 218 a period).
- **Trades and the budget invested per year** (the candidates check's last part, item 52), stocks traded in brackets: *Winners at a discount* 2011 34 (12), 2012 22 (10), 2013 4, 2015 11, 2016 29 (12), 2017 10, 2018 10, 2019 31 (12), 2020 29 (16), 2023 45 (18), 2024 15; *The comeback* 2011 48 (14), 2012 12, 2015 42 (12), 2016 18, 2019 48 (18), 2020 12, 2023 48 (21), 2024 12. Each trade is €100.
- ***Bear markets*' All** is its only traded period (2019–22) against the fund over the same period, so like is compared with like; the simulator's own *All* averages the index over all four.
- The texts' stories now give numbers; the POC's period stories are gone (other periods, other events).

### What explains the figures (2026-11-26)

Looked into with Ernest before the figures went live:

- ***Index trend* and *Bear markets*: the years.** 2011–2026 has no long bear market. Over 1999–2026 (SPY, in dollars, since Yahoo's EUR/USD rate starts in 2004), *Index trend* makes +10.1% a year against +8.7% held, with a worst year of −20.8% against −36.8% (2008). After 2011 the falls were short and V-shaped, and it bought back higher each time; in 2022 a whipsaw (sold 386, rebought 433, sold 399) and euro cash while the dollar rose made it worse than holding. *Bear markets* over 1999–2026 makes +5.7% against +8.7%: it bought 2001–02 too early.
- **The stock templates: the choice of stocks.** Against baseline 2 (the same days and money on the whole draft) they lose 2.3 (*Winners at a discount*) and 2.8 (*The comeback*) points a year; timing adds a little; the draft (equal-weighted) trails the S&P 500 (cap-weighted) only in 2023–26 (3.2 points). The money goes to the first signals (all spent 13–27 months into each period), and the 5 trades a stock come within about 70 days, mostly at higher prices, so 60 trades sit in 17–29 stocks.
- **On the starter lists** (2023–26, chosen today, so with hindsight): *Winners at a discount* +47.4% against +39.5% for its own list bought on the same days (+7.9 points) and +17.3% for the S&P 500; *The comeback* +35.1% against +42.8%. On 16 years, Dip beats its list in 3 periods of 4.
- **2 trades a stock** (on the draft): *The comeback* +15.4% a year against +12.7% with 5, ahead of the S&P 500 (+14.4%); *Winners at a discount* unchanged (+12.2%). *The comeback*'s default becomes 2 trades of 200 (Ernest, 2026-11-26): +15.3% a year, worst year −15.9%, ahead of the index in 3 periods of 4; its figures are those since.
- Next: a test draft of the strategies' kind of stocks (`ROADMAP.md` item 59).

## Biases

| Bias | How the backtest handles it |
| --- | --- |
| Look-ahead in prices | Signals at the close, trades at the next open; the warm-up reads only past prices |
| Look-ahead in the stocks | The draft follows the S&P 500 as it stood at each period's start; the own list and one stock default to the latest period |
| Survivorship | Counted per period when the draft is drawn; removed only by a paid data source |
| Tuning to the past | Every period is shown, not only All |
| Luck of the draw | One large draft, by sector and size, checked against all members |
| Unequal money or costs | The same money, limit, costs and formulas for every line |
| Backtest ≠ app | The same engine; simulations use the same costs; the monthly limit becomes a strategy setting |

## In the app

- **The monthly limit is a strategy setting** (built, item 57): "Each month at most [€x]", part of the budget, with unspent limit carried over in full, by the same rule as the backtest (`MonthlyLimit.Room`, shared). No limit is the default and what every earlier strategy has (null, the same as x = the budget). Set on *Set your budget*, changed on a running strategy from its page (*Change the budget* isn't built). Not for *Index trend*: all in, it has no trades to pace, as its tested figures have no limit (Ernest, 2026-11-26). The daily check and AI rounds make no signal past the month's room; a strategy's backtest uses its limit.
- **Simulations use the backtest's costs** (decided, not built): fee 0.1% + spread 0.1% a trade, no fixed part, for new trades from the change on (past trades keep the fee they were recorded with). Manual strategies keep the fee the user enters (`ROADMAP.md` item 41).

## Open points

- Tapping a baseline to add its line to the chart (item 55 left it out).
