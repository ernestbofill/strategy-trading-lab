# Backtest Research Findings & Template Harmonization

This document records the research findings from the Proof of Concept (POC) repository (`ernestbofill/trading-strategies-bot/backend/research` and `docs/strategy-review-*.md`) and how they were applied to establish honest, standardized backtest figures for Strategy Trading Lab (Roadmap Item 15, PR #119).

---

## 1. Principles of Strategy Backtesting in STL

### 1.1 Strategy Across Universes vs Single-Asset Backtests
In Strategy Trading Lab, template backtest figures do **not** represent a backtest tied to a single asset chosen in isolation. Rather, they evaluate how the strategy rules perform across broad multi-asset universes over historical market regimes (2000–2026):
- **Stock-based strategies** (*Winners at a discount*, *The comeback*): Evaluated over a broad universe of 1,146 US equities (S&P 500, S&P 400 Mid-Cap, Nasdaq-100, and historic constituents).
- **Bear market accumulation** (*Bear markets*): Evaluated across 134 historic bear-market drawdowns in major equity indexes and broad ETFs (SPY, QQQ, DIA, IWM, and sector SPDRs).
- **Index trend-following** (*Index trend*): Evaluated across 6 major global equity indexes (S&P 500, Nasdaq-100, Developed ex-US, DAX, Nikkei 225, and Vanguard 500).

### 1.2 Independent Test Periods & Direct Alpha on Deployed Capital
Each time period presented in *How we tested* (`2000-12`, `2013-19`, `2020-26`, `All`) is an **independent test starting with fresh capital** at the start of that period, reported as **yearly averages**:
- For step-based strategies (*Winners at a discount*, *The comeback*, *Bear markets*), each step buy is paired with an identical dollar purchase of the S&P 500 (SPY) on the exact same day (`direct alpha`), measuring the return on capital actually deployed rather than penalizing uninvested cash waiting for signals.
- For *Index trend* (all-in / all-out), the full compounding account is compared against buying and holding the target index across 6 global indexes selectable via tabs.

### 1.3 Honest Benchmarking & Worst-Year Drawdowns
- All equity strategies are benchmarked against an S&P 500 fund (SPY) receiving the exact same capital allocations on the same days, held without selling.
- Every screen (the review's Tested box, the *By period* table, the strategy page's past result) shows each return **a year, on average**, alongside the template's and the index's **worst calendar year return** (`worstYear` and `indexWorstYear`), direct **alpha** (with an explanatory `InfoTip`), and an optional **story** column.

---

## 2. Harmonized Template Backtest Figures

The four templates with algorithmic backtests use the PoC research regimes:
- **2000–2012** (Dot-com collapse, 2003–07 bull run, 2008 Great Financial Crisis, European debt crisis)
- **2013–2019** (Low-rate expansion, prolonged bull market with zero 20% S&P 500 crashes)
- **2020–2026** (COVID-19 shock, rapid recovery, 2022 inflation bear market, 2025 correction)
- **All (2000–2026)** (Full multi-regime backtest)

| Template | Period | Strategy (/yr) | S&P 500 (/yr) | Worst Year (Tmpl / Index) | Alpha (/yr) | Story / Notes |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Winners at a discount** (`dip`) | 2000–12<br>2013–19<br>2020–26<br>**All** | +11.5%<br>+14.9%<br>+16.3%<br>**+14.3%** | +5.5%<br>+13.4%<br>+14.3%<br>**+9.8%** | -18% / -37%<br>-12% / -4%<br>-21% / -18%<br>**-21% / -37%** | +6.0%<br>+1.5%<br>+2.0%<br>**+4.5%** | 1,146 US stocks (never-sell review). Step buying during pullbacks from 60-day peaks. |
| **The comeback** (`comeback`) | 2000–12<br>2013–19<br>2020–26<br>**All** | +15.1%<br>+16.3%<br>+19.2%<br>**+16.5%** | +5.5%<br>+13.4%<br>+14.3%<br>**+9.8%** | -15% / -37%<br>-14% / -4%<br>-24% / -18%<br>**-24% / -37%** | +9.6%<br>+2.9%<br>+4.9%<br>**+6.7%** | 1,146 US stocks (+15% buy line, 8% chase limit). Turnaround buying on beaten-down stocks. |
| **Bear markets** (`bear`) | 2000–12<br>2013–19<br>2020–26<br>**All** | +9.4%<br>*—*<br>+16.8%<br>**+11.2%** | +1.7%<br>*—*<br>+13.3%<br>**+7.7%** | -12% / -37%<br>*—*<br>-14% / -18%<br>**-14% / -37%** | +7.7%<br>*—*<br>+3.5%<br>**+3.5%** | `2013-19` displays *"No market crashes"* because the S&P 500 never fell 20%. |
| **Index trend** (`trend`, S&P 500 tab) | 2000–12<br>2013–19<br>2020–26<br>**All** | +6.4%<br>+10.4%<br>+11.2%<br>**+10.2%** | +1.7%<br>+13.8%<br>+13.3%<br>**+10.9%** | -4% / -37%<br>-11% / -4%<br>-16% / -18%<br>**-16% / -37%** | +4.7%<br>-3.4%<br>-2.1%<br>**-0.7%** | Includes tabs for S&P 500, Nasdaq-100, Developed ex-US, DAX, Nikkei 225, and Vanguard 500. |

*Note: "Your AI's top picks" (`ai`) has no algorithmic backtest figures because AI round scores depend on subjective external LLM prompts run on-demand.*

---

## 3. Key Findings on Strategy Behavior

### 3.1 Dip and Turnaround Strategies
- **Drawdown Protection in Crises**: Both `dip` (+48%) and `comeback` (+62%) strongly outperformed the index (-8%) during 2000–2009 because cash was deployed only during deep discounts rather than remaining fully invested through bear markets.
- **Lagging During Relentless Bull Runs**: During 2010–2019, buying only dips (+185% and +210%) trailed 100% buy-and-hold (+246%) because strong bull runs rarely triggered pullbacks deep enough to deploy the full cash balance.

### 3.2 Bear Market Accumulation
- **Patience and Capital Preservation**: *The big market drop* stays largely in cash during tranquil bull regimes (hence +18% during 2010–2019 vs +246% for SPY).
- **Crash Exploitation**: When rare major drawdowns occur (2000–02, 2008, 2020, 2022), phased stepped accumulation captures significant recovery upside with minimal drawdown risk (worst year was only -12% in 2008 vs -37% for the market).

### 3.3 Index Trend
- **Major Crash Avoidance**: *Ride the long trend* suffered almost no loss (-2%) in 2008 because the SMA 200 exit triggered before the crash deepened.
- **Whipsaws in Sideways Markets**: During choppy or range-bound years (such as 2011 or 2015-16), trend following suffered minor whipsaw losses from false breakouts.

---

## 4. UI/UX Implementation Details

1. **Independent Periods Explanation**:
   Underneath the *By period* table on `/new/templates/:key/how-tested`, a caption clarifies:
   > *"All figures are yearly averages. Each period is an independent test that starts with fresh money."*
   *(Catalan: "Totes les xifres són mitjanes anuals. Cada període és una prova independent que comença amb diners nous."*)

2. **Survivor Bias Caveat**:
   The cautionary notice explains:
   > *"Historical tests use companies and indexes trading today. Failed or delisted companies from decades ago aren't included, which flatters past results."*

3. **Handling Customized Strategies & Custom Asset Lists**:
   - The backend checks whether any of a strategy's parameters differ from template defaults, or if its active assets contain any stock outside the starter list (or a fund other than the default fund).
   - If customized, `PastResult.IsCustomized` is returned as `true`.
   - The dashboard card and strategy detail page display the disclaimer:
     > *"With the tested settings and stocks, the template did..."*
     *(Catalan: "Amb la configuració i els valors provats, la plantilla va fer un...")*
     in place of *"In the past, [years], the template did..."* until personal on-demand backtests can be computed.
