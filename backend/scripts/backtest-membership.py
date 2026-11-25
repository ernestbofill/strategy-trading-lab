#!/usr/bin/env python3
"""Regenerates the backtest's S&P 500 membership files (docs/backtest.md, *Data*).

Writes, to backend/src/StrategyTradingLab.Api/Backtest/Data/:

- sp500-membership.csv: ticker,start,end. The ticker was in the index from `start` (inclusive) to `end` (exclusive, empty:
  still in it), from fja05680/sp500's "S&P 500 Historical Components & Changes (Updated).csv" (MIT licence). One row of
  that file is the index from its date until the next row's date; this script checks that the intervals give back every row.
- wikipedia-current.csv: ticker,name. Wikipedia's "List of S&P 500 companies" today (CC BY-SA 4.0).
- wikipedia-changes.csv: date,added,added_name,removed,removed_name. Wikipedia's "Historical components of the S&P 500"
  changes table (CC BY-SA 4.0): the second list the membership is checked against, and the names of removed tickers.
- sources.json: what was read (fja's last row, the Wikipedia revisions), so a draw records which membership it used.

The symbol map (symbol-map.csv) is written by hand: see Backtest/Data/README.md.

Run it from anywhere: python3 backend/scripts/backtest-membership.py
"""

import csv
import datetime
import io
import json
import re
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "src" / "StrategyTradingLab.Api" / "Backtest" / "Data"
FJA = "https://raw.githubusercontent.com/fja05680/sp500/master/" + urllib.parse.quote("S&P 500 Historical Components & Changes (Updated).csv")
WIKI = "https://en.wikipedia.org/w/"
CURRENT_PAGE = "List of S&P 500 companies"
CHANGES_PAGE = "Historical components of the S&P 500"
TICKER = re.compile(r"[A-Z][A-Z0-9.\-]*")


def get(url: str) -> str:
    """The page's text. Wikipedia answers 429 when it is asked too often: wait as it says (or a minute) and try again, 3 times."""
    request = urllib.request.Request(url, headers={"User-Agent": "membership-script/1.0"})
    for attempt in range(4):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                return response.read().decode("utf-8")
        except urllib.error.HTTPError as error:
            if error.code != 429 or attempt == 3:
                raise
            wait = int(error.headers.get("Retry-After") or 60)
            print(f"Too many requests to {urllib.parse.urlsplit(url).netloc}: trying again in {wait} s.")
            time.sleep(wait)
    raise AssertionError("unreachable")


def membership() -> tuple[list[tuple[str, str, str]], str]:
    rows = [(date, sorted(set(t for t in tickers.split(",") if t))) for date, tickers in list(csv.reader(io.StringIO(get(FJA))))[1:]]
    rows.sort()
    intervals: list[tuple[str, str, str]] = []
    open_since: dict[str, str] = {}
    for date, tickers in rows:
        now = set(tickers)
        for ticker in sorted(set(open_since) - now):
            intervals.append((ticker, open_since.pop(ticker), date))
        for ticker in sorted(now - set(open_since)):
            open_since[ticker] = date
    intervals += [(ticker, start, "") for ticker, start in open_since.items()]
    intervals.sort()
    # Every row must come back from the intervals.
    for date, tickers in rows:
        rebuilt = sorted(t for t, start, end in intervals if start <= date and (end == "" or date < end))
        if rebuilt != tickers:
            raise SystemExit(f"The intervals don't give back fja's row of {date}.")
    return intervals, rows[-1][0]


def page(title: str) -> tuple[str, int, str]:
    """The page's wikitext, with its revision id and time (Wikipedia's REST endpoint: one call, no API rate limit)."""
    answer = json.loads(get(WIKI + "rest.php/v1/page/" + urllib.parse.quote(title.replace(" ", "_"), safe="")))
    return answer["source"], answer["latest"]["id"], answer["latest"]["timestamp"]


def clean(cell: str) -> str:
    cell = re.sub(r"<ref[^>]*/>", "", cell)
    cell = re.sub(r"<ref.*?</ref>", "", cell, flags=re.S)
    cell = re.sub(r"\[\[(?:[^|\]]*\|)?([^\]]*)\]\]", r"\1", cell)
    cell = re.sub(r"\{\{[^}]*\}\}", "", cell)
    return cell.replace("&amp;", "&").strip()


def ticker(cell: str) -> str:
    match = TICKER.match(cell.strip())
    return match.group(0) if match else ""


def table_rows(text: str, table_id: str) -> list[list[str]]:
    start = text.index(f'id="{table_id}"')
    body = text[start:text.index("\n|}", start)]
    rows = []
    for row in body.split("\n|-")[1:]:
        lines = [line for line in row.strip("\n").split("\n") if line.startswith("|")]
        cells: list[str] = []
        for line in lines:
            cells += (line[2:] if line.startswith("||") else line[1:]).split("||")
        if cells:
            rows.append(cells)
    return rows


def current(text: str) -> list[tuple[str, str]]:
    members = []
    for cells in table_rows(text, "constituents"):
        symbol = re.search(r"\{\{\s*[\w ]*(?:Symbol|link)\s*\|\s*([^}|]+)", cells[0], re.I)
        if symbol and len(cells) > 1:
            members.append((symbol.group(1).strip(), clean(cells[1])))
    if len(members) < 490:
        raise SystemExit(f"Only {len(members)} current members read from Wikipedia: the page's table changed.")
    return sorted(members)


def changes(text: str) -> list[tuple[str, str, str, str, str]]:
    found = []
    for cells in table_rows(text, "changes"):
        cells = [clean(c) for c in cells]
        if len(cells) < 5:
            continue
        try:
            date = datetime.datetime.strptime(re.sub(r"^.*\|", "", cells[0]).replace(",", "").strip(), "%B %d %Y").date()
        except ValueError:
            continue  # The header rows.
        found.append((date.isoformat(), ticker(cells[1]), cells[2] if ticker(cells[1]) else "", ticker(cells[3]), cells[4] if ticker(cells[3]) else ""))
    if len(found) < 300:
        raise SystemExit(f"Only {len(found)} changes read from Wikipedia: the page's table changed.")
    return sorted(found, reverse=True)


def write(name: str, header: list[str], rows) -> None:
    with open(OUT / name, "w", newline="", encoding="utf-8") as file:
        writer = csv.writer(file, lineterminator="\n")
        writer.writerow(header)
        writer.writerows(rows)


def main() -> None:
    intervals, last_row = membership()
    current_text, current_rev, current_at = page(CURRENT_PAGE)
    changes_text, changes_rev, changes_at = page(CHANGES_PAGE)
    write("sp500-membership.csv", ["ticker", "start", "end"], intervals)
    write("wikipedia-current.csv", ["ticker", "name"], current(current_text))
    write("wikipedia-changes.csv", ["date", "added", "added_name", "removed", "removed_name"], changes(changes_text))
    sources = {
        "fja05680/sp500": {"file": "S&P 500 Historical Components & Changes (Updated).csv", "lastRow": last_row},
        "wikipedia": {
            CURRENT_PAGE: {"revision": current_rev, "at": current_at},
            CHANGES_PAGE: {"revision": changes_rev, "at": changes_at},
        },
    }
    (OUT / "sources.json").write_text(json.dumps(sources, indent=2) + "\n", encoding="utf-8")
    print(f"{len(intervals)} intervals to {last_row}; Wikipedia revisions {current_rev} and {changes_rev}.")


if __name__ == "__main__":
    main()
