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
| Frontend (Vitest) | 3 | 12 | 15 |
| Backend (xUnit) | 3 | 26 | 29 |
| **All** | **6** | **38** | **44** |

## Log

Newest first. *Where* is `local` or `CI`. *Test* is the file and test name (or the shared part, when several failed for one cause).

| Date | PR | Where | Suite | Test | Tests | Verdict | Cause and fix |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2026-11-24 | #282 | local | Frontend (Vitest) | `src/App.test.tsx > links with a language > keeps a signed-in account's language over the link's` | 1 | FP | Failed once in the full run, passed alone; this PR doesn't touch it. Not sure of the cause: likely state leaking between test files. No fix. |
| 2026-11-24 | #282 | local | Backend (xUnit) | `NeonBranchTests.Database_is_postgres_15_or_later_with_the_migration_applied` | 1 | FP | The local test database lacked the new `DropManualSignalTrades` migration; passed once `migrations.sql` was applied to it. Machine-dependent. |
| 2026-11-24 | #282 | local | Backend (xUnit) | `NotificationTests.A_second_signal_for_the_same_stock_the_same_day_is_not_alerted_and_an_alerted_buy_that_ends_is_told_once` | 1 | TP | The "buy signal ended" query called `MovedOn` inside a join, which EF couldn't translate (it threw); fixed with `NotMovedOn`. The test also needed its newer signal removed before the end part: that signal now moves the missed one on, so no alert is due, on purpose. |
| 2026-11-24 | #282 | local | Backend (xUnit) | `StrategyPageTests` (a simulation's page; a live signal waiting) | 2 | FP | They read `results.rules` and `rulesSteps`, removed on purpose with "The rules alone". Assertions updated. |
| 2026-11-24 | #282 | local | Frontend (Vitest) | `StrategyPage.test.tsx > shows a live strategy next to the index…`, `SignalPage.test.tsx > skips with a reason…` | 2 | FP | Expected "The rules alone" and the old skip note, changed on purpose. Assertions updated. |
| 2026-11-24 | #282 | local | Backend (xUnit) | `NeonBranchTests` (object storage, health) and `AdminDatabaseTests.Switching_…` | 3 | FP | They need RustFS object storage, which a cloud session lacks (they pass in CI). No fix: machine-dependent. |
| 2026-11-24 | #281 | local | Frontend (Vitest) | `src/features/strategy/StrategyPage.test.tsx` (the budget line, "12 of 30 steps") | 1 | FP | Copy changed on purpose (*step* → *trade*, issue #277); the expectation now reads "12 of 30 trades". |
| 2026-11-24 | #281 | local | Backend (xUnit) | `AiAnswerReaderTests.Reads_a_table_in_catalan_and_words_its_problems_in_catalan` | 1 | FP | Copy changed on purpose (Catalan *pas* → *operació*, issue #277); the expectation updated. |
| 2026-11-24 | #280 | local | Backend (xUnit) | `NeonBranchTests` (object storage, health) and `AdminDatabaseTests.Switching_to_a_database_that_answers_moves_the_api_there_and_is_kept` | 3 | FP | They need RustFS object storage, which a cloud session lacks (they pass in CI). No fix: machine-dependent. |
| 2026-11-24 | #280 | CI | Backend (xUnit) | `StrategyEvaluatorTests.A_trend_strategy_buys_all_in_sells_all_out_and_reinvests_the_proceeds_on_the_next_buy` | 1 | FP | Its rebuy closed at 120, about 19% above the SMA 200, past the new chase limit (intended). The rebuy closes at 105 now, inside the buy window. |
| 2026-11-24 | #280 | local | Backend (xUnit) | `RuleChecksTests.Index_trend` (trend-SPY-0) | 1 | FP | A new assertion that a past-the-chase-limit day exists; that data set has none. Removed: `TrendRulesTests` covers it. |
| 2026-11-24 | #280 | local | Backend (xUnit) | `TrendRulesTests.A_buy_signal_closes_when_the_price_falls_back_below_the_buy_line_while_a_held_position_stays_held` | 1 | FP | Out of the market the trend now has a second buy check (`chase`), so `Checks.Single()` picks the `buyLine` check by key. |
| 2026-11-22 | #271 | local | Backend (xUnit) | `DipRulesTests.The_comeback_measures_the_turn_from_the_recent_low_window` | 1 | FP | Its falling case came from 120, above the buy area (118), so it now reads as a pullback from above (intended) and has no volume check. It falls from 116, inside the buy area, instead. |
| 2026-11-22 | #268 | local | Frontend (Vitest) | `stockLine.test.ts` (2), `priceChartMarks.test.ts` (2), `StockPage.test.tsx` (1): the comeback's milestone cases | 5 | FP | The milestone code was removed on purpose (Ernest); the tests now cover the pace instead. |
| 2026-11-21 | #268 | local | Backend (xUnit) | `RuleChecksTests.The_comeback` (NOW, MOD) | 2 | TP | Once every step was bought, the new pace row was marked used with no word on whether it bought a step, which the stock page reads as "Step bought". Fixed in the engine: a full position has no pace row. |
| 2026-11-21 | #268 | local | Backend (xUnit) | `NeonBranchTests` (object storage, health) and `AdminDatabaseTests.Switching_to_a_database_that_answers_moves_the_api_there_and_is_kept` | 3 | FP | They need RustFS object storage, which a cloud session lacks (they pass in CI). No fix: machine-dependent. |
| 2026-11-21 | #267 | local | Backend (xUnit) | `NeonBranchTests` (object storage, health) and `AdminDatabaseTests.Switching_to_a_database_that_answers_moves_the_api_there_and_is_kept` | 3 | FP | They need RustFS object storage, which a cloud session lacks (they pass in CI). No fix: machine-dependent. |
| 2026-11-21 | #266 | local | Backend (xUnit) | `NeonBranchTests` (object storage, health) and `AdminDatabaseTests.Switching_to_a_database_that_answers_moves_the_api_there_and_is_kept` | 3 | FP | They need RustFS object storage, which a cloud session lacks (they pass in CI). No fix: machine-dependent. |
| 2026-11-17 | #263 | local | Frontend (Vitest) | `src/features/feedback/FeedbackSection.test.tsx > lists open items, filters closed/all, …` | 1 | FP | The new Validate step clicked the first closed item's button, not the second's; the test picked the wrong button. |
| 2026-11-16 | #259 | local | Backend (xUnit) | `EngineGoldenTests.Dip_in_winners_matches_the_POC_day_by_day` (dip-ETN-a) | 1 | FP | *Proven growth* now takes its low up to the peak's day (intended, not the POC's rule): ETN's 12 crash days of March 2020 read not a candidate. The golden runs keep the POC's rule (the params' default: `LowBeforePeakFrom` is off unless the template turns it on). |
| 2026-11-16 | #259 | local | Backend (xUnit) | `NeonBranchTests` (object storage, health) and `AdminDatabaseTests.Switching_to_a_database_that_answers_moves_the_api_there_and_is_kept` | 3 | FP | They need RustFS object storage, which a cloud session lacks (they pass in CI). No fix: machine-dependent. |
| 2026-11-16 | #257 | local | Backend (xUnit) | `EngineGoldenTests.The_comeback_matches_the_POC_day_by_day` (VICR, one other) | 2 | FP | The comeback's volume now counts only on a rising price (intended, not the POC's rule): two signal-ended reasons differed. The golden runs set `VolumeOnRiseFrom` past their last day. |
| 2026-11-16 | #257 | local | Backend (xUnit) | `DipRulesTests.The_comeback_measures_the_turn_from_the_recent_low_window` | 1 | FP | Same change: its signal day closed below the close 5 days before. The test's prices now rise into the signal, and it also covers the falling case. |
| 2026-11-16 | #255 | local | Backend (xUnit) | every `[NeonFact]` class that opens the database (AccountTests, AdminAccessTests, StockChangeTests…) | most of the suite | FP | The local PostgreSQL had stopped when the cloud session's worker restarted (connection refused). No code fix: started it again, and the suite passed. |
| 2026-11-16 | #246 | local | Frontend (Vitest) | `src/lib/push.test.ts` (all four) | 4 | FP | Loading Testing Library inside the shared `afterEach` ran while the test's stubbed `navigator` (no `userAgent`) was still in place; `testSetup.ts` now loads it up front, in jsdom files only. |
| 2026-11-16 | #246 | local | Frontend (Vitest) | `src/features/summary/SummaryPage.test.tsx > renders quiet state on days with no changes and shows running strategies scoreboard` | 1 | FP | The new SIM check looked for a simulation row in the quiet-day test, but the row had gone into the active-day test's data; moved the check there. |
| 2026-11-16 | #261 | local | Backend (xUnit) | `StrategyEvaluatorTests.A_stock_ticked_again_gets_no_signal_for_the_days_it_was_off_the_list` | 1 | FP | Intended change: the replay no longer holds an unsignalled step from the days off the list, so the window still open on the first day back signals then (as for a stock just added); still no back-dated signal. Expectation updated. |
| 2026-11-15 | #241 | local | Frontend (Vitest) | `src/features/admin/AdminTemplates.test.tsx` (saves a new version; makes an older one active) and `AdminAssets.test.tsx > says when an asset already there is hidden, and a failed reload apart from the add` | 3 | TP | Moving the admin screens to `useLoadable` dropped two behaviours: Templates no longer showed the save's reply, Assets no longer said its reload failed. Added `update(key, change)` and made `reload(key)` resolve to whether it loaded. |
| 2026-11-15 | #241 | local | Frontend (Vitest) | `src/features/admin/AdminAlerts.test.tsx > shows no error when a dismiss is refused because the alert is no longer open` | 1 | TP | The first `useLoadable` draft showed an error for a 404 on an alert a run had cleared; the error now shows only while the alert is still open. |
| 2026-11-15 | #241 | local | Frontend (Vitest) | `src/features/admin/AdminUsers.test.tsx`, `AdminAlerts.test.tsx`, `AdminInvites.test.tsx` (the second test of each file) | 4 | FP | Results kept by `useLoadable` leaked from one test to the next; `testSetup.ts` now empties the cache after each test. |
| 2026-11-15 | #238, #239 | local | Backend (xUnit) | `NeonBranchTests` (object storage, health) and `AdminDatabaseTests.Switching_to_a_database_that_answers_moves_the_api_there_and_is_kept` | 3 | FP | They need RustFS object storage, which a cloud session lacks (they pass in CI). No fix: machine-dependent. |
| 2026-11-14 | #236 | local | Frontend (Vitest) | `src/features/strategy/StockPage.test.tsx > shows window closed, >100% progress, moving-average stack strips, and past-zone cancellation` | 1 | TP | Legacy pre-deploy `buyLine` check (`-13%` vs buy line `$187.28`, low `$162.85`) rounded back to `+0.1%` above the low instead of `+0%` due to 0.1% rounding in stored `value`; snapped `< 0.15%` to `0` in `normalizeCheck`. |
