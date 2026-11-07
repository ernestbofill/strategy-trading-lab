# Backtest Research Findings & Template Harmonization

This document records the research findings from the Proof of Concept (POC) repository (`ernestbofill/trading-strategies-bot/backend/research` and `docs/strategy-review-*.md`) and how they were applied to establish honest, standardized backtest figures for Strategy Trading Lab (Roadmap Item 15, PR #119).

---

## 1. Principles of Strategy Backtesting in STL

### 1.1 Strategy Across Universes vs Single-Asset Backtests
In Strategy Trading Lab, template backtest figures do **not** represent a backtest tied to a single asset chosen in isolation. Rather, they evaluate how the strategy rules perform across broad multi-asset universes over historical market regimes (2000–2024):
- **Stock-based strategies** (*Winners at a discount*, *The comeback*): Evaluated over a broad universe of 1,146 US equities (S&P 500, S&P 400 Mid-Cap, Nasdaq-100, and historic constituents).
- **Bear market accumulation** (*The big market drop*): Evaluated across 134 historic bear-market drawdowns in major equity indexes and broad ETFs (SPY, QQQ, DIA, IWM, and sector SPDRs).
- **Index trend-following** (*Ride the long trend*): Evaluated across 6 major global equity indexes (S&P 500, Nasdaq 100, EuroStoxx 50, Nikkei 225, MSCI World, and Emerging Markets).

### 1.2 Independent Test Periods
Each time period presented in *How we tested* (`2000-09`, `2010-19`, `2020-24`) is an **independent test starting with fresh capital** (e.g., €3,000) at the start of that period.
- Because each period starts fresh without carrying over accumulated gains or losses from prior decades, the returns of individual sub-periods do not sum or compound arithmetically to the `All (2000-24)` figure.
- Presenting independent decade-scale regimes tests whether the strategy was consistently sound or only profited from a single lucky market cycle.

### 1.3 Honest Benchmarking & Worst-Year Drawdowns
- All equity strategies are benchmarked against an S&P 500 fund (SPY) receiving the exact same capital allocations on the same days, held without selling.
- In accordance with STL design rules, every backtest disclosure displays the strategy return, benchmark index return, and the single **worst calendar year return** (`worstYear`) to ensure users observe downside risk alongside returns.
- The review's Tested box and the *By period* table show each return **a year, on average**: the yearly rate that compounds to the period's return (e.g. +490% over 25 years is +7.4% a year), so 5- and 10-year periods compare. They show the S&P 500 fund's worst calendar year (`indexWorstYear`, SPY total return) next to the template's: 2000-09 −37% (2008), 2010-19 −5% (2018), 2020-24 −18% (2022), All −37% (2008).

---

## 2. Harmonized Template Backtest Figures

The four templates with algorithmic backtests use standardized test intervals:
- **2000–2009** (Dot-com collapse, 2003–07 bull run, 2008 Great Financial Crisis)
- **2010–2019** (Low-rate expansion, prolonged bull market)
- **2020–2024** (COVID-19 shock, rapid recovery, 2022 inflation bear market)
- **All (2000–2024)** (Full 25-year multi-regime backtest)

| Template | Period | Strategy Return | S&P 500 (SPY) | Worst Calendar Year | Universe & Method |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **Winners at a discount** (`dip`) | 2000–09<br>2010–19<br>2020–24<br>**All (2000–24)** | +48%<br>+185%<br>+72%<br>**+490%** | -8%<br>+246%<br>+96%<br>**+538%** | -18% (2008)<br>-12% (2018)<br>-21% (2022)<br>**-21% (2022)** | 1,146 US stocks (S&P 500, S&P 400, Nasdaq-100, and dropouts). Tranche buying during pullbacks from 60-day peaks. |
| **The comeback** (`comeback`) | 2000–09<br>2010–19<br>2020–24<br>**All (2000–24)** | +62%<br>+210%<br>+88%<br>**+540%** | -8%<br>+246%<br>+96%<br>**+538%** | -15% (2008)<br>-14% (2018)<br>-24% (2022)<br>**-24% (2022)** | 1,146 US stocks. Turnaround buying on oversold stocks rebounding on volume. |
| **The big market drop** (`bear`) | 2000–09<br>2010–19<br>2020–24<br>**All (2000–24)** | +36%<br>+18%<br>+58%<br>**+165%** | -8%<br>+246%<br>+96%<br>**+538%** | -12% (2008)<br>-4% (2018)<br>-18% (2022)<br>**-18% (2022)** | 134 historical bear markets across major indexes and ETFs. Capital preservation during normal markets; buys phased drops. |
| **Ride the long trend** (`trend`) | 2000–09<br>2010–19<br>2020–24<br>**All (2000–24)** | +77%<br>+127%<br>+61%<br>**+572%** | -8%<br>+246%<br>+96%<br>**+538%** | -2% (2008)<br>-11% (2018)<br>-21% (2022)<br>**-21% (2022)** | 6 global indexes (S&P 500, Nasdaq-100, EuroStoxx 50, Nikkei 225, MSCI World, EM). Trend following with SMA 200. |

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
   > *"Each period is an independent test starting with fresh money at its beginning, which is why the periods don't add up to All."*
   *(Catalan: "Cada període és una prova independent amb diners nous a l'inici, per això els períodes no sumen el total («All»)."*)

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
