# Winners at a discount: is "Averages stacked" the right trend rule?

A study from 2026-11-16, after Ernest's feedback that very few stocks seemed to have their averages stacked. It led to two averages replacing the three for every strategy (#267, `docs/decisions.md`). The script is [dip-trend-rule-study.py](dip-trend-rule-study.py).

## The question

A stock is a candidate while it has proven growth, its averages are stacked and its long average is rising. The POC's stack is SMA 50 > SMA 150 > SMA 200. Is that too strict, and what would a looser rule change?

## Method

- **Stocks:** the template's 9 starter stocks (COHR, ETN, MOD, MPWR, NVT, SU.PA, SMCI, VRT, VICR) and the 13 other stocks in the dev database (1211.HK, AAPL, CBRAIN.CO, CRM, GROW.L, MRNA, MSFT, NOW, PLTR, SNOW, TSLA, ZETA, ZS).
- **Prices:** Yahoo's adjusted daily closes from 2000 (or the listing) to 2026-11-13: 370 stock-years.
- **Rules:** the template's defaults, with the low taken up to the peak's day (#259). Only the trend rule changes between variants; proven growth (50%), the discount zone (5–15% under the 60-day peak) and the SMA 50 floor stay.
- **A signal** is the first day a stock is a candidate in its zone, at least 10 days after the last one.
- **After a signal:** the close 63, 126 and 252 trading days later against the signal day's close.

It is not the engine and not a backtest: no steps, budget, cooldown from fills or next-open prices, and no index fund to compare with.

## Results

All 22 stocks, 370 stock-years. "6 months after" is the change over the next 126 trading days.

| Trend rule | Days a candidate | Signals | vs now | Average 6 months after | Up after 6 months | Worst tenth, 6 months |
| --- | --- | --- | --- | --- | --- | --- |
| **Before:** SMA 50 > 150 > 200, SMA 200 rising | 30.0% | 1,387 | – | +23.5% | 64% | −29.5% |
| SMA 150 > 200, SMA 200 rising | 32.2% | 1,441 | +4% | +23.5% | 65% | −29.5% |
| **Now:** SMA 50 > 200, SMA 200 rising | 34.1% | 1,543 | +11% | +22.8% | 64% | −30.0% |
| Price > SMA 200, SMA 200 rising | 32.6% | 1,581 | +14% | +22.8% | 65% | −30.2% |
| Price > SMA 150 > 200, SMA 200 rising | 28.1% | 1,436 | +4% | +23.5% | 65% | −29.6% |
| No trend rule (growth only) | 43.5% | 1,910 | +38% | +23.7% | 65% | −30.0% |
| Any day, no rule at all | | | | +16.1% | 64% | −25.9% |

The starter list alone (185 stock-years) reads the same: 668 signals before, 749 with two averages (+12%), +21.2% and +21.5% six months after.

By period (signals, average 6 months after, share up):

| Period | Three averages | Two averages | No trend rule |
| --- | --- | --- | --- |
| 2000–2002 | 17 · −17.7% · 6% | 25 · −10.8% · 20% | 50 · −9.7% · 24% |
| 2003–2007 | 198 · +17.1% · 69% | 216 · +18.0% · 71% | 237 · +19.7% · 73% |
| 2008–2009 | 79 · +39.3% · 72% | 91 · +30.7% · 66% | 160 · +34.1% · 74% |
| 2010–2019 | 383 · +13.2% · 57% | 422 · +13.1% · 58% | 487 · +13.3% · 58% |
| 2020–2022 | 288 · +29.9% · 77% | 318 · +28.7% · 76% | 387 · +31.3% · 75% |
| 2023–2026 | 367 · +31.4% · 60% | 407 · +31.0% · 62% | 503 · +29.8% · 62% |

On 2026-11-13, the starter list:

- Only SMCI was a candidate with three averages; SMCI and NVT with two.
- None would have signalled under any variant, even with no trend rule: eight were 20–48% under their peak, far past the zone and under their SMA 50. MPWR was in the zone with a 48.4% growth, short of the 50% needed.

## What it says

1. **The stack is not rare.** These stocks were candidates on 30% of days with three averages.
2. **Loosening it changes little.** Two averages give about 11% more signals, with the same results after them.
3. **The quiet list of November 2026 was not the stack's doing.** The nine starter stocks are one theme (AI power and data centres) and fell together, out of the zone. Stocks from other sectors would do more than any trend variant.
4. **The strict rule buys less in bad markets.** In 2000–2002 and 2008–2009 it gave far fewer signals; those it let through were no better on average.

## Limits

- **Survivors.** These 22 stocks are on the lists because they did well. The sample can't show the trend rule's main job, keeping out stocks that never recover. Results after a signal look good under every variant for that reason.
- **Small and correlated.** 22 stocks, many from one theme.
- **Not the engine.** See *Method*.

## Decision and what is left

Ernest chose two averages (2026-11-16). #260 first offered it as a choice on Advanced settings; to simplify, it replaced the three for every strategy instead, with no setting (2026-11-21, #267). Still to do: backtest the two-average rule over the template's periods (Milestone 4, STL's own backtest). Until then the template's results are the three-average rule's, and How we tested says so.
