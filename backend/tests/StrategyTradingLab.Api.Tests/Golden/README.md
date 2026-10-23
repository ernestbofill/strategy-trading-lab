# Golden files

The POC's own results (`trading-strategies-bot`) on real stored Yahoo candles, which the engine must reproduce day by day.

- `dip-*.json`: *Leader pullback* over a slice of one stock's history, as the POC's backtest runs it (a signal is bought at the next open), with a 100 step, 5 steps and a 10-day cooldown (the STL spec's; the POC's default is 20). Per day: the status, the step of a buy signal and the days a signal starts.
- `gen_dip.py` makes them from the POC repo, which holds the candles in `backend/data/trading.db` (usage in its header). The five files are `ETN 900 dip-ETN-a.json 1000`, `ETN 900 dip-ETN-b.json 3000`, `MPWR 900 dip-MPWR.json 1000`, `NOW 900 dip-NOW.json 2000` and `CRM 900 dip-CRM.json 0` (symbol, days, file, days before the end). Only the closes are kept: the rules read nothing else.
- No slice reaches *Signal ended*: in the POC's backtest, as in the engine, a signal is acted on the next day, so a window never closes unanswered. Those branches of `RulesRunner` are kept for parity and are not exercised.
