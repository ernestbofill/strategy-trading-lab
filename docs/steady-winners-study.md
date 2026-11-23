# Steady winners: which candidate rule finds steady growers?

A study from 2026-11-23, after Ernest's feedback that *Winners at a discount* never makes his steady growers candidates. It led to the *Steady winners* template in `ROADMAP.md` (item 49). The script is [steady-winners-study.py](steady-winners-study.py).

## The question

*Winners at a discount* makes a stock a candidate when it rose at least 50% from its low in the last ~8 months (168 trading days). That rule finds leaders that just ran up (VRT, COHR, VICR), not stocks that grow 15–25% a year. Over the 2 years of prices in production, SU.PA rose at most 41% in any 8 months and never passed it, although its SMA 50 was above its SMA 200 on 88% of the days. ETN passed on 24% of the days, BRK-B on none.

The discount zone fails them too: a steady stock's price sits only 2–3% above its SMA 50, so a 5% dip already falls through the floor. With the SMA 50 floor, SU.PA's zone was empty (the floor above the zone's top) on 30% of its uptrend days over 4 years.

## Method

- **Stocks:** Ernest's dip list (ETN, SU.PA, NVT, VRT, COHR, MOD, MPWR, VICR, SMCI, CBRAIN.CO), steady names (MSFT, COST, V, MA, LIN, BRK-B, AAPL, GOOG, JNJ, KO, RSG, WM, CTAS, ORLY), slow growers (PG, ADP, SPGI) and volatile ones (ASML, NVDA, and more in the chat). SPY stands for the S&P 500.
- **Prices:** Yahoo's adjusted daily closes, 10 years to 2026-11-20.
- **Each of the last 2 years' days** is checked with its own window ending that day, so a result doesn't hang on one start date.

It is not the engine and not a backtest: no steps, budget, cooldown or next-open prices, and nothing on what happened after a signal.

## What was tried, and what was kept

| Question | Tried | Kept, and why |
| --- | --- | --- |
| Growth | 10% a year on average; 10% every year; faster than the S&P 500 | **Faster than the S&P 500 on average.** Every year at 10% leaves almost nothing (ETN had 3 years under 10% in 5, COST 2). A fixed 10% lets in stocks the index fund beats. |
| Steadiness | Above the SMA 200 on ≥75% of days; worst fall ≤35%; worst fall vs the S&P 500's | **Worst fall at most 15 points deeper than the S&P 500's worst fall in the same years.** The share of days above the SMA 200 let leaders in (VRT 88%, NVT 84%) and kept MSFT (70%) and COST (73%) out. A fixed 35% shuts the template off for years after a crash: over 5 years it dropped SU.PA, MSFT and GOOG because of 2022. |
| When the falls happen | The stock's fall during the S&P 500's worst fall (± months); its worst fall at any time | **At any time in the window.** A stock's own fall in a calm market is what shows it isn't steady: ADP fell 41% from Jun 2025 to Apr 2026, but only 13% during the S&P 500's worst fall. |
| Years | 3, 4, 5 | **5 by default, a setting from 3 to 5.** Five years usually hold a bear market, so the fall rule tests a real downturn (the last 4 years' worst S&P 500 fall was only −19%). Neither length clearly won: most of the difference is where the window starts. |
| Trend now | – | **SMA 200 rising**, as *Winners at a discount*. |
| Signal | 5–15% below the 60-day high, SMA 50 floor; 3–10%, SMA 100 floor | **3–10% below the 60-day high, above the SMA 100.** SU.PA's buy days went from 9% to 18% of its uptrend days, ETN's from 14% to 27%; the leaders barely change. |

## Results

The kept rule: growth faster than the S&P 500, worst fall at most 15 points deeper than the S&P 500's, SMA 200 rising. "Candidate" and "buy" are the share of the last 2 years' days the stock was a candidate, and a candidate in the 3–10% / SMA 100 zone.

| Stock | 4 years: candidate | buy | 5 years: candidate | buy | Candidate on 2026-11-20 (5 years) |
| --- | --- | --- | --- | --- | --- |
| ETN | 55% | 17% | 85% | 31% | yes |
| SU.PA | 64% | 26% | 66% | 27% | – (grew 1 point a year slower than the S&P 500; SMA 200 falling) |
| NVT | 7% | 1% | 0% | 0% | – |
| VRT, COHR, MOD, MPWR, VICR, SMCI, CBRAIN.CO | 0% | 0% | 0–3% | 0% | – |
| MSFT | 39% | 12% | 50% | 12% | – |
| COST | 64% | 17% | 77% | 17% | yes |
| V | 36% | 13% | 5% | 2% | yes |
| MA | 37% | 7% | 6% | 1% | yes |
| LIN | 44% | 9% | 49% | 12% | yes |
| BRK-B | 55% | 12% | 61% | 12% | yes |
| AAPL | 61% | 17% | 78% | 23% | yes |
| GOOG | 26% | 4% | 13% | 4% | – |
| JNJ, KO | 3–8% | 0–2% | 7–10% | 2–3% | yes |
| RSG, WM, CTAS | 41–50% | 9–11% | 40–59% | 9–17% | yes |
| ORLY | 73% | 21% | 73% | 21% | – |
| ADP | 40% | 15% | 28% | 10% | – |
| PG, SPGI, ASML, NVDA | 0% | 0% | 0% | 0% | – |

## What it shows

- **It splits leaders from steady stocks.** Of Ernest's dip list only ETN and SU.PA ever qualify; the leaders stay with *Winners at a discount*.
- **Beating the S&P 500 is a high bar.** SU.PA grew slower than the index and fell harder over 4 years (15% vs 16% a year, −31% vs −19%), so it isn't a candidate today; a stock the index fund beats isn't what this template is for (Ernest).
- **Start dates matter.** V and MA barely qualify over 5 years in the last 2 years because those windows began in 2019–21, when they lagged; over 4 years they do. That is why the years are a setting.

## Limits

- Adjusted closes include dividends; if the app's closes don't, dividend payers read 1–3 points a year lower.
- The S&P 500 is the yardstick for every stock, European ones too, as it is for every strategy's results today.
- No backtest: the template ships without tested results until STL's own backtest (Milestone 4), or that backtest comes first.
