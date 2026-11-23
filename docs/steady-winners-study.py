"""The check behind docs/steady-winners-study.md: which stocks the proposed Steady winners candidate rule picks.

Usage: python3 steady-winners-study.py DIR, where DIR holds one Yahoo chart file per symbol (SYMBOL.json), SPY included, from
https://query1.finance.yahoo.com/v8/finance/chart/SYMBOL?range=10y&interval=1d&events=split
It reads the adjusted closes only. A study, not the engine: no steps, budget, cooldown or next-open prices.
"""
import json, sys, bisect

D = sys.argv[1]
STOCKS = ['ETN', 'SU.PA', 'NVT', 'VRT', 'COHR', 'MOD', 'MPWR', 'VICR', 'SMCI', 'CBRAIN.CO',  # Ernest's dip list
          'MSFT', 'COST', 'V', 'MA', 'LIN', 'BRK-B', 'AAPL', 'GOOG', 'JNJ', 'KO', 'RSG', 'WM', 'CTAS', 'ORLY',
          'PG', 'ADP', 'SPGI', 'ASML', 'NVDA']
DAYS = 504  # the last 2 years of trading days, each checked with its own window


def load(s):
    r = json.load(open(f'{D}/{s}.json'))['chart']['result'][0]
    p = [(t, v) for t, v in zip(r['timestamp'], r['indicators']['adjclose'][0]['adjclose']) if v]
    return [t for t, _ in p], [v for _, v in p]


def worst_fall(seg):
    peak, dd = seg[0], 0.0
    for v in seg:
        peak = max(peak, v)
        dd = min(dd, v / peak - 1)
    return dd


def per_year(seg, t0, t1):
    return (seg[-1] / seg[0]) ** (365.25 * 86400 / (t1 - t0)) - 1


def sma(c, i, k):
    return sum(c[i - k + 1:i + 1]) / k


spy_t, spy_c = load('SPY')


def spy_window(t0, t1):
    a, b = bisect.bisect_left(spy_t, t0), bisect.bisect_right(spy_t, t1)
    return spy_c[a:b], spy_t[a], spy_t[b - 1]


def candidate(t, c, i, years):
    """Beats the S&P 500's growth, falls at most 15 points deeper than it, and its 200-day average is rising."""
    n = 252 * years
    seg = c[i - n + 1:i + 1]
    spy, s0, s1 = spy_window(t[i - n + 1], t[i])
    return (per_year(seg, t[i - n + 1], t[i]) >= per_year(spy, s0, s1)
            and worst_fall(seg) >= worst_fall(spy) - 0.15
            and sma(c, i, 200) > sma(c, i - 20, 200))


def in_zone(c, i):
    """The signal rule: 3-10% below the 60-day high, and above the 100-day average."""
    peak = max(c[i - 59:i + 1])
    return max(peak * 0.90, sma(c, i, 100)) <= c[i] <= peak * 0.97


print(f"{'stock':10}{'4y cand':>9}{'4y buy':>8}{'5y cand':>9}{'5y buy':>8}{'today 5y':>10}   (% of the last 2 years' days)")
for s in STOCKS:
    t, c = load(s)
    row = []
    for years in (4, 5):
        cand = buy = 0
        for i in range(len(c) - DAYS, len(c)):
            if candidate(t, c, i, years):
                cand += 1
                buy += in_zone(c, i)
        row += [cand * 100 // DAYS, buy * 100 // DAYS]
    today = 'yes' if candidate(t, c, len(c) - 1, 5) else '-'
    print(f"{s:10}{row[0]:>8}%{row[1]:>7}%{row[2]:>8}%{row[3]:>7}%{today:>10}")
