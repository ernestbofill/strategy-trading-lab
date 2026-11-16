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
| Frontend (Vitest) | 3 | 2 | 5 |
| Backend (xUnit) | 0 | 1 | 1 |
| **All** | **3** | **3** | **6** |

## Log

Newest first. *Where* is `local` or `CI`. *Test* is the file and test name (or the shared part, when several failed for one cause).

| Date | PR | Where | Suite | Test | Tests | Verdict | Cause and fix |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2026-11-16 | #246 | local | Frontend (Vitest) | `src/features/summary/SummaryPage.test.tsx > renders quiet state on days with no changes and shows running strategies scoreboard` | 1 | FP | The new SIM check looked for a simulation row in the quiet-day test, but the row had gone into the active-day test's data; moved the check there. |
| 2026-11-15 | #241 | local | Frontend (Vitest) | `src/features/admin/AdminTemplates.test.tsx` (saves a new version; makes an older one active) and `AdminAssets.test.tsx > says when an asset already there is hidden, and a failed reload apart from the add` | 3 | TP | Moving the admin screens to `useLoadable` dropped two behaviours: Templates no longer showed the save's reply, Assets no longer said its reload failed. Added `update(key, change)` and made `reload(key)` resolve to whether it loaded. |
| 2026-11-15 | #241 | local | Frontend (Vitest) | `src/features/admin/AdminAlerts.test.tsx > shows no error when a dismiss is refused because the alert is no longer open` | 1 | TP | The first `useLoadable` draft showed an error for a 404 on an alert a run had cleared; the error now shows only while the alert is still open. |
| 2026-11-15 | #241 | local | Frontend (Vitest) | `src/features/admin/AdminUsers.test.tsx`, `AdminAlerts.test.tsx`, `AdminInvites.test.tsx` (the second test of each file) | 4 | FP | Results kept by `useLoadable` leaked from one test to the next; `testSetup.ts` now empties the cache after each test. |
| 2026-11-15 | #238, #239 | local | Backend (xUnit) | `NeonBranchTests` (object storage, health) and `AdminDatabaseTests.Switching_to_a_database_that_answers_moves_the_api_there_and_is_kept` | 3 | FP | They need RustFS object storage, which a cloud session lacks (they pass in CI). No fix: machine-dependent. |
| 2026-11-14 | #236 | local | Frontend (Vitest) | `src/features/strategy/StockPage.test.tsx > shows window closed, >100% progress, moving-average stack strips, and past-zone cancellation` | 1 | TP | Legacy pre-deploy `buyLine` check (`-13%` vs buy line `$187.28`, low `$162.85`) rounded back to `+0.1%` above the low instead of `+0%` due to 0.1% rounding in stored `value`; snapped `< 0.15%` to `0` in `normalizeCheck`. |
