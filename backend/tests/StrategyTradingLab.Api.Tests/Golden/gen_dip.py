"""Runs the POC's Leader pullback over real stored candles, as its backtest does (every signal is acted on at the next
open), and writes the per-day status, step and new-buy flag. Usage: gen_dip.py SYMBOL DAYS OUT.json"""
import json, sqlite3, sys
from datetime import date, timedelta
import polars as pl
sys.path.insert(0, "/Users/boyracoon/Dev/trading-strategies-bot/backend/src")
from trading_bot.strategies.stage2_pullback import Stage2Pullback, Stage2Params
from trading_bot.strategies.base import Acted, Position, TradeLot, Status

symbol, days, out = sys.argv[1], int(sys.argv[2]), sys.argv[3]
end = int(sys.argv[4]) if len(sys.argv) > 4 else 0
db = sqlite3.connect("/Users/boyracoon/Dev/trading-strategies-bot/backend/data/trading.db")
rows = db.execute("select c.day,c.open,c.high,c.low,c.close,c.volume from daily_candles c join instruments i on i.id=c.instrument_id "
                  "where i.symbol=? and c.source='yahoo' order by c.day", (symbol,)).fetchall()
rows = rows[max(len(rows)-end-days,0):len(rows)-end]
df = pl.DataFrame({"day":[date.fromisoformat(r[0]) for r in rows],"open":[r[1] for r in rows],"high":[r[2] for r in rows],
                   "low":[r[3] for r in rows],"close":[r[4] for r in rows],"volume":[float(r[5] or 0) for r in rows]})
strategy = Stage2Pullback()
# STL defaults: step 100, 5 steps, cooldown 10 days (the STL spec; the POC default is 20).
p = Stage2Params(position_target=500, tranche_size=100, cooldown_days=10)
prepared = strategy.prepare(df, p)
state, position, lots, pending, signal_day, acted, result = {}, None, [], False, None, None, []
for i, row in enumerate(prepared.iter_rows(named=True)):
    day, open_, close = row["day"], row["open"], row["close"]
    if pending:
        lots.append(TradeLot(day=day, price=open_, amount=100.0, peak_close=open_))
        position = Position(entry_day=lots[0].day, entry_price=sum(l.price*l.amount for l in lots)/sum(l.amount for l in lots),
                            peak_close=max(l.peak_close for l in lots), invested=sum(l.amount for l in lots),
                            last_buy_day=day, sell_rules=False, lots=lots)
        acted = Acted(signal_day=signal_day, day=day, kind="buy")
        pending = False
    if position is not None:
        position = position.advance(close)
    ev = strategy.step(i, row, state, position, p, acted)
    if ev.new_buy and ev.tranche is not None:
        pending, signal_day = True, day
    result.append({"date": day.isoformat(), "status": ev.status.value, "step": ev.tranche.number if ev.tranche else None,
                   "newBuy": bool(ev.new_buy), "reason": ev.reason})
bars = [{"date": r[0], "open": r[1], "high": r[2], "low": r[3], "close": r[4], "volume": int(r[5] or 0)} for r in rows]
json.dump({"symbol": symbol, "bars": bars, "days": result}, open(out, "w"), separators=(",", ":"))
counts = {}
for d in result: counts[d["status"]] = counts.get(d["status"], 0) + 1
print(symbol, len(rows), counts, "buys:", sum(d["newBuy"] for d in result))
