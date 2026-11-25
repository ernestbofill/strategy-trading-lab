# The backtest's S&P 500 membership

The S&P 500's members on any date, for drawing the test draft ([docs/backtest.md](../../../../../docs/backtest.md), *Data*). Embedded in the API; a draw records the files' hash, so the owner page says when they changed after the draw.

| File | What | Source | Licence |
| --- | --- | --- | --- |
| `sp500-membership.csv` | `ticker,start,end`: in the index from `start` (inclusive) to `end` (exclusive; empty: still in it) | [fja05680/sp500](https://github.com/fja05680/sp500), *S&P 500 Historical Components & Changes (Updated).csv* | MIT, © 2019-2020 Farrell J. Aultman |
| `wikipedia-current.csv` | `ticker,name`: the members today | Wikipedia, [List of S&P 500 companies](https://en.wikipedia.org/wiki/List_of_S%26P_500_companies) | CC BY-SA 4.0 |
| `wikipedia-changes.csv` | `date,added,added_name,removed,removed_name`: the index changes Wikipedia lists | Wikipedia, [Historical components of the S&P 500](https://en.wikipedia.org/wiki/Historical_components_of_the_S%26P_500) | CC BY-SA 4.0 |
| `sources.json` | fja's last row and the Wikipedia revisions read | | |
| `symbol-map.csv` | `ticker,until,yahoo,note`: written by hand (below) | | |
| `sectors.csv` | `symbol,sector,note`: written by hand, Yahoo's sector names for listings its search no longer finds while their prices are still there (delisted or renamed lately); used only when Yahoo gives no sector | | |

Regenerate the first four with `python3 backend/scripts/backtest-membership.py`, and redraw the test draft if a period start's members changed.

**The second list.** Wikipedia's current list, walked back through its changes table, gives another membership on each date. The draw compares the two at each period start and lists the differences in the report. They are mostly renames (Wikipedia records a rename as no change, fja as a change of ticker) and changes Wikipedia leaves out. The names in the Wikipedia files are also what Yahoo's name for a symbol is checked against.

**The symbol map.** fja writes a ticker as it was on the day; Yahoo keeps a company's history under its ticker of today, or drops it when the company is gone. Without a row here, a ticker is asked of Yahoo as it is, with `.` written `-` (`BRK.B` → `BRK-B`). A row applies to the dates before `until` (empty: every date):

- `yahoo` set: the company's ticker on Yahoo today (`FB` → `META`).
- `yahoo` empty: the ticker belongs to another company today, so it is not asked of Yahoo at all (`S` was Sprint and is SentinelOne). The member counts as missing.

A ticker with no row whose Yahoo name doesn't match its Wikipedia name is left out of the draw and listed in the report, so a reused ticker never brings another company's prices. Fix it with a row here.
