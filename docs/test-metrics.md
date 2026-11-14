# Test metrics

How useful our tests are: every test failure, and whether it caught a real problem. Started 2026-11-13 (Ernest). Over time this shows which tests earn their CI time and which only cost upkeep, so we can drop or rewrite the noisy ones.

## How to record

Whoever sees a test fail (an AI tool or Ernest) adds a row to the log, in the same PR as the fix, committed with it. This is bookkeeping on runs that happen anyway: never run tests extra times for it.

- **What counts:** a test that ran and failed, locally or in CI, on a branch you are working on. A build or compile error before any test runs, or a CI job that never reached the tests (runner outage, cancelled run), doesn't count.
- **One row per failing test per cause.** Re-running the same failure while fixing it doesn't add rows. Ten tests failing for one cause are one row, with the count in *Tests*.
- **True positive (TP):** the test caught a real problem: the fix is in the product code (or its config, migrations, data), not in the test.
- **False positive (FP):** the product code was right, and the fix is in the test itself. For example: expectations updated after an intended change in behaviour or copy, a brittle selector, a flaky or timing-dependent test, a test that depends on the machine (`.env.local`, time zone, clock).
- **Both changed:** if the failure revealed a real bug, it's a TP even when the test needed updating too. If it only failed because behaviour was changed on purpose, it's an FP.
- **Not sure** (a flake that went away on a rerun nobody did for this purpose, a cause you couldn't find): log it as FP and say so in *Cause and fix*.
- Update the totals below with each row.

## Totals

| Suite | TP | FP | Total |
| --- | --- | --- | --- |
| Frontend (Vitest) | 1 | 0 | 1 |
| Backend (xUnit) | 0 | 0 | 0 |
| **All** | **1** | **0** | **1** |

## Log

Newest first. *Where* is `local` or `CI`. *Test* is the file and test name (or the shared part, when several failed for one cause).

| Date | PR | Where | Suite | Test | Tests | Verdict | Cause and fix |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2026-11-14 | #236 | local | Frontend (Vitest) | `src/features/strategy/StockPage.test.tsx > shows window closed, >100% progress, moving-average stack strips, and past-zone cancellation` | 1 | TP | Legacy pre-deploy `buyLine` check (`-13%` vs buy line `$187.28`, low `$162.85`) rounded back to `+0.1%` above the low instead of `+0%` due to 0.1% rounding in stored `value`; snapped `< 0.15%` to `0` in `normalizeCheck`. |
