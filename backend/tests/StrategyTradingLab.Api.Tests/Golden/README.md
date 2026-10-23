# Golden files

The POC's own results (`trading-strategies-bot`) on real stored Yahoo candles, which the engine must reproduce day by day.

- `dip-*.json`: *Leader pullback* over a slice of one stock's history, as the POC's backtest runs it (a signal is bought at the next open), with a 100 step, 5 steps and a 10-day cooldown (the STL spec's; the POC's default is 20). Per day: the status, the step of a buy signal and the days a signal starts.
- `gen_dip.py` makes them: `backend/.venv/bin/python gen_dip.py SYMBOL DAYS OUT.json [DAYS_BEFORE_END]` in the POC repo, which holds the candles in `backend/data/trading.db`. Only the closes are kept: the rules read nothing else.
