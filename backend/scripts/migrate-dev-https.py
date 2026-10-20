#!/usr/bin/env python3
"""Applies backend/migrations.sql to the Neon dev branch over HTTPS.

For Claude Code cloud sessions, where port 5432 is blocked, so `dotnet ef database update` can't connect.
Locally use `dotnet ef database update`, or just run the API from its launch profile, which migrates the dev database itself.

The script is EF's idempotent one, so running it again changes nothing. It is split into single statements
(Neon's HTTPS endpoint takes one statement per query) and sent as one batch, which Neon runs as one transaction.
DATABASE_URL (or DATABASE_URL_UNPOOLED) must be the dev branch's: never run this against production.
"""
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from urllib.parse import urlparse

SCRIPT = Path(__file__).resolve().parent.parent / "migrations.sql"


def statements(sql: str) -> list[str]:
    """The idempotent script as single statements, without its START TRANSACTION / COMMIT lines."""
    sql = sql.lstrip("﻿")
    blocks = re.findall(r"DO \$EF\$.*?END \$EF\$;", sql, flags=re.S)
    rest = re.sub(r"DO \$EF\$.*?END \$EF\$;", "", sql, flags=re.S)
    rest = re.sub(r"^(START TRANSACTION|COMMIT);$", "", rest, flags=re.M)
    history = re.fullmatch(r'\s*(CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" \(.*?\n\);)\s*', rest, flags=re.S)
    if not history:
        sys.exit("migrations.sql has a shape this script doesn't know: expected the history table and DO $EF$ blocks only.")
    return [history.group(1), *blocks]


def main() -> None:
    url = os.environ.get("DATABASE_URL_UNPOOLED") or os.environ.get("DATABASE_URL")
    if not url:
        sys.exit("No DATABASE_URL: the Neon dev variables aren't set, so there is no dev database to migrate.")
    host = urlparse(url).hostname
    queries = [{"query": s, "params": []} for s in statements(SCRIPT.read_text(encoding="utf-8"))]

    with tempfile.TemporaryDirectory() as tmp:
        headers, body = Path(tmp, "headers"), Path(tmp, "body.json")
        # The connection string goes through files, so it is never on a command line or in the output.
        headers.write_text(f"Neon-Connection-String: {url}\nContent-Type: application/json\n")
        body.write_text(json.dumps({"queries": queries}))
        result = subprocess.run(
            ["curl", "-sS", "-m", "120", "-X", "POST", f"https://{host}/sql", "-H", f"@{headers}",
             "--data-binary", f"@{body}", "-w", "\n%{http_code}"],
            capture_output=True, text=True)

    text, _, status = result.stdout.rpartition("\n")
    if result.returncode != 0 or status != "200":
        message = (json.loads(text).get("message") if text.startswith("{") else text) or result.stderr
        sys.exit(f"Migrating the dev database failed (HTTP {status or '?'}): {message}")
    print(f"Dev database is up to date: {len(queries) - 1} migration steps checked.")


if __name__ == "__main__":
    main()
