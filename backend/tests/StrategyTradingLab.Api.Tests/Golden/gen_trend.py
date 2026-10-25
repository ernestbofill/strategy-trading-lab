"""Runs the POC's Index trend over real stored candles, as its backtest does (every buy or sell signal is acted on at the
next open), and writes the golden file the engine tests read: closes, per-day status, step, reason and the days a buy or
sell signal starts.

Usage, with the POC's own Python (its .venv): POC_REPO=/path/to/trading-strategies-bot \
  $POC_REPO/backend/.venv/bin/python gen_trend.py SYMBOL DAYS OUT.json [DAYS_BEFORE_END]
"""
import json, os, sqlite3, sys
from datetime import date
import polars as pl
POC = os.environ["POC_REPO"]
sys.path.insert(0, os.path.join(POC, "backend", "src"))
from trading_bot.strategies.index_trend import IndexTrend, IndexTrendParams
from trading_bot.strategies.base import Acted, Position, TradeLot

symbol, days, out = sys.argv[1], int(sys.argv[2]), sys.argv[3]
end = int(sys.argv[4]) if len(sys.argv) > 4 else 0
db_path = os.path.join(POC, "backend", "data", "trading.db").replace("\\", "/")
db = sqlite3.connect(f"file:{db_path}?mode=ro&immutable=1", uri=True)
rows = db.execute("select c.day,c.open,c.high,c.low,c.close,c.volume from daily_candles c join instruments i on i.id=c.instrument_id "
                  "where i.symbol=? and c.source='yahoo' order by c.day", (symbol,)).fetchall()
rows = rows[max(len(rows)-end-days,0):len(rows)-end]
df = pl.DataFrame({"day":[date.fromisoformat(r[0]) for r in rows],"open":[r[1] for r in rows],"high":[r[2] for r in rows],
                   "low":[r[3] for r in rows],"close":[r[4] for r in rows],"volume":[float(r[5] or 0) for r in rows]})
strategy = IndexTrend()
p = IndexTrendParams(position_target=3000, tranche_size=3000)
prepared = strategy.prepare(df, p)
state, position, lots, pending_buy, pending_sell, signal_day, acted, result = {}, None, [], False, False, None, None, []
for i, row in enumerate(prepared.iter_rows(named=True)):
    day, open_, close = row["day"], row["open"], row["close"]
    if pending_buy:
        lots.append(TradeLot(day=day, price=open_, amount=3000.0, peak_close=open_))
        position = Position(entry_day=lots[0].day, entry_price=open_, peak_close=open_, invested=3000.0,
                            last_buy_day=day, sell_rules=True, lots=lots)
        acted = Acted(signal_day=signal_day, day=day, kind="buy")
        pending_buy = False
    elif pending_sell:
        lots = []
        position = None
        acted = Acted(signal_day=signal_day, day=day, kind="sell")
        pending_sell = False
    if position is not None:
        position = position.advance(close)
    ev = strategy.step(i, row, state, position, p, acted)
    if ev.new_buy and ev.tranche is not None:
        pending_buy, signal_day = True, day
    if ev.new_sell:
        pending_sell, signal_day = True, day
    result.append({"date": day.isoformat(), "status": ev.status.value, "step": ev.tranche.number if ev.tranche else None,
                   "newBuy": bool(ev.new_buy), "newSell": bool(ev.new_sell), "reason": ev.reason})
out_doc = {"symbol": symbol, "dates": [r[0] for r in rows], "close": [r[4] for r in rows],
           "status": [d["status"] for d in result], "step": [d["step"] or 0 for d in result],
           "reason": [d["reason"] for d in result], "newBuy": [i for i, d in enumerate(result) if d["newBuy"]],
           "newSell": [i for i, d in enumerate(result) if d["newSell"]]}
json.dump(out_doc, open(out, "w"), separators=(",", ":"))
counts = {}
for d in result: counts[d["status"]] = counts.get(d["status"], 0) + 1
print(symbol, len(rows), counts, "buys:", sum(d["newBuy"] for d in result), "sells:", sum(d["newSell"] for d in result))
