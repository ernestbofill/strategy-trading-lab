# QA state

What the next session reads first. The agent updates it at the end of every session. Ernest may edit anything here, for example to point the next session at an area.

## Handoff

*Last session:* 2026-11-06-1, the breadth session, v0.52.0-alpha, `qaagent`.

**No S1 open.** F-001 (a hand-logged sale larger than the holding shows a false gain, +258.8% in the test) was lowered to S3 by Ernest, because it comes from a user's inconsistent entry. What the app should do is still open; L-003 has what is left to try.

**Next session, do first:** L-001 and L-002, because only time tests them. No fixture had a daily check yet, so every rules pill, the stock pages' rules (E2) and the summary charts are unseen. Compare with *Fixtures* below. AI rounds fall due every day, so run them. The ZS signal on `QA fixture · AI · Live` was snoozed to Nov 9, but should have ended on Nov 7: check it went *Missed* and what reminder went out.

**How to sign in:** Chrome with Claude in Chrome was already signed in as `qaagent`. The built-in browser has no saved login and offered no autofill. Don't expect Claude to type a password on production (L-006).

## Account baseline

**Account:** `qaagent` (README, *The account's label*). Another account gets its own copy of this section and of *Fixtures*, under its label.

The account's settings before any session changed them. Restore them before ending.

| Setting | Value |
| --- | --- |
| Language | English |
| Currency | USD |
| Signals and reminders: App / Email | On / On |
| Summary: Email | On |
| This device (push) | Off for this device (Chrome) |
| AI data sharing | Off. Answered "no" on the first round |
| Strategies not starting with `QA ` (leave alone) | None |

The browser's `stl.pushAsked` is set in that Chrome, because Confirm's "Allow notifications" was unticked: the ask stays unticked there.

## Fixtures

Long-running `QA` strategies that stay from session to session, so that what happens over days (daily checks, signals, statuses, missed signals, AI rounds due, results) can be tested. Never archive them. At most 6, so there is room to create throwaway ones under the 10-running cap. All in USD, the account's currency, $3,000 budget; steps of $100 × 5, except as noted.

| Name | Template | Mode | Created | Notes: state on 2026-11-06, evening |
| --- | --- | --- | --- | --- |
| QA fixture · Discount · Sim | Winners at a discount | Simulation | 2026-11-06 | 9 stocks, all NOT CHECKED, no trades, $0 used |
| QA fixture · Comeback · Live | The comeback | Live | 2026-11-06 | 16 stocks, all NOT CHECKED. One hand-logged buy: 1 ZS at $200, fee $1.40. +1.2%, $201 used |
| QA fixture · Bear · Sim | Bear markets | Simulation | 2026-11-06 | iShares Core S&P 500 UCITS ETF, steps of $600 × 5, no trades |
| QA fixture · Index · Live | Index trend | Live | 2026-11-06 | iShares Core S&P 500 UCITS ETF, all in, no trades |
| QA fixture · AI · Sim | Your AI's top picks | Simulation | 2026-11-06 | AI round every 1 day. Round 1 done: Eaton at $335.90 (close Nov 5 $327.02). −1.2%, $100 used. Next due Nov 7 |
| QA fixture · AI · Live | Your AI's top picks | Live | 2026-11-06 | AI round every 1 day. Round 1: SU.PA did, 0.385 at €228.50, rate 0.891, $98.80, no fee, −0.3%. Extra round: ZS waiting, snoozed to Nov 9. Next due Nov 7 |

## Lead queue

Open leads to follow, most important first. **next** means the next session takes it before new charters. When a lead is done, delete it here (the log keeps its history).

| ID | Area | What to try | From | Priority |
| --- | --- | --- | --- | --- |
| L-001 | F3, F4, G4 | The ZS extra-round signal on `AI · Live`, snoozed to Nov 9, should end when the round is due on Nov 7 (a Saturday). Did it go *Missed*? Does the reminder still go out, or a "buy signal ended" alert? Then: can a missed signal still record a trade? Are daily rounds really due on non-market days? | 2026-11-06-1 C2.17 | **next** |
| L-002 | D3, E2, C1, D2, X9 | First daily check on all fixtures: every template's pills and detail lines, stock page rules and gauges, charts on cards and pages, AI "bought at the next open" row (F-007), *The rules alone* on `AI · Live`, what the summary email says. Compare with *Fixtures* | 2026-11-06-1 | **next** |
| L-003 | D6, D14 | Follow F-001: a sale with no buy at all; editing a buy down below what was sold; deleting a buy after a sale; an oversold stock's row; the same in Confirm a sell signal | 2026-11-06-1 C3.7 | |
| L-004 | G5 | "Picks vs the rest": does "the rest –" fill in once the others have prices? Does the figure stay ahead of the strategy's own? (F-020) | 2026-11-06-1 C2.10 | |
| L-005 | X3 | Signed-in screens at 390px and 360px, long names, 200% zoom. Chrome's window wouldn't resize below desktop; try the built-in browser with a saved login, or ask Ernest | 2026-11-06-1 C5 | |
| L-006 | A4 | Real log-in, log-out and coming back to the page asked for, and the wrong-password check with `qa-nobody@stl.invalid`. Claude can't type a password on production, not even a made-up one, so these need Ernest, another tool or a saved login in the browser. **Log-out signs out the browser the session depends on** | 2026-11-06-1 C4.8 | |
| L-007 | X5, X8, H5, B3 | Two tabs and a stale tab acting on changed data; going offline; select text → 💬 Add comment; adding a stock or fund the app doesn't have; the 60-stock limit; Back through the flow and after Start | 2026-11-06-1 | |
| L-008 | D9, B4, B5, G6 | Save a changed setting on a running strategy; Advanced and How we tested on Comeback, Bear and Trend (F-010 there?); edit and save a prompt; AI model "Other…"; the fee, amount and rate fields with malformed numbers (F-003) | 2026-11-06-1 | |
| L-009 | X6 | Signed-out access to a signed-in page (Log-in, then back). A signal or stock ID from another strategy of the same account | 2026-11-06-1 | |

## Coverage map

Depth: **untested** → **smoke** (happy path seen and checked against the spec) → **deep** (variations, edge cases and the oracles done, its leads followed) → **regressed** (re-checked after a fix or a change to the area). A row drops back to **smoke** when the area changes (a merged PR).

Areas come from the spec ([§4–7](../docs/strategy-trading-lab-app-spec.md)). Add a row when a feature ships; mark one **removed** when it goes.

### A · Public and sign-in

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| A1 | Welcome (`/`, `/welcome`), guest, its preview widgets | §6 Welcome | smoke | 2026-11-06-1 | F-021. C4.1–2 |
| A2 | Guest new-strategy flow (`/new` without an account) | §6 Looking around | smoke | 2026-11-06-1 | Draft survives a reload. F-011. C4.6 |
| A3 | Sign-up form: validation, invite code lookup | §6 Sign-up | smoke | 2026-11-06-1 | Invite-only mode. Password rules not typed. Never submit it (README). C4.3 |
| A4 | Log-in, log-out, coming back to the page asked for | §6 Log-in | untested | 2026-11-06-1 | Page read only: see L-006. Wrong password only with `qa-nobody@stl.invalid` |
| A5 | Forgot password | §6 Forgot | smoke | 2026-11-06-1 | Only `qa-nobody@stl.invalid`. C4.4 |
| A6 | Legal pages, the footer, the site map | §6, CLAUDE.md | smoke | 2026-11-06-1 | Disclaimer and About read. F-015. C4.7 |
| A7 | Language links (`/ca/…`), unknown addresses | §4 | smoke | 2026-11-06-1 | F-012. C4.5 |

### B · New strategy flow

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| B1 | Pick a template (count of strategies already from it) | §6.1 | smoke | 2026-11-06-1 | F-019. C1.1 |
| B2a | Review: Winners at a discount | §6.2 | smoke | 2026-11-06-1 | F-009. C1.2 |
| B2b | Review: The comeback | §6.2 | smoke | 2026-11-06-1 | F-009. C1.2 |
| B2c | Review: Bear markets (fund picker) | §6.2 | smoke | 2026-11-06-1 | Picker opened, no search. F-009 |
| B2d | Review: Index trend | §6.2 | smoke | 2026-11-06-1 | F-009. C1.2 |
| B2e | Review: Your AI's top picks (sources, prompt) | §6.2 | smoke | 2026-11-06-1 | F-008, F-009. C1.2–3 |
| B3 | Stock list on the review: tick, untick, + Add a stock | §6.2 | smoke | 2026-11-06-1 | Search only. Adding a new stock writes to shared assets: only a well-known symbol. C1.5 |
| B4 | Advanced settings, per template | §6.2b | smoke | 2026-11-06-1 | Winners and AI only. C1.6 |
| B5 | How we tested | §6.2c | smoke | 2026-11-06-1 | Winners only. F-010. C1.7 |
| B6 | Prompt editor (AI draft) | §6.2 | untested | | |
| B7 | Choose the mode (Live needs a confirmed email) | §6.3 | smoke | 2026-11-06-1 | Unconfirmed email not reachable with this account. C1.8 |
| B8 | Set your budget: budget, step, max steps, currency | §6.4 | deep | 2026-11-06-1 | F-002, F-008. C1.9–13 |
| B9 | Name, preview and confirm: name rules, Edit links, notifications ask | §6.5 | smoke | 2026-11-06-1 | C1.14–17 |
| B10 | The draft across reloads, Back, and leaving and coming back | §4 | smoke | 2026-11-06-1 | Reload and direct address only. C1.18 |

### C · Dashboard

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| C1 | Strategy cards: result, index fund, worst year, chart, budget bar | §6 Dashboard | smoke | 2026-11-06-1 | F-017. C3.1, C3.3 |
| C2 | Waiting for you: live signals, AI rounds due | §6 Dashboard | smoke | 2026-11-06-1 | C3.2 |
| C3 | The cap of 10 running strategies | §6 Dashboard | smoke | 2026-11-06-1 | Reach it with throwaways, then clean up. C3.14 |
| C4 | Archived section | §6 Dashboard | smoke | 2026-11-06-1 | C3.15 |

### D · Strategy page

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| D1 | Summary box, Simulation | §6 Strategy page | smoke | 2026-11-06-1 | No chart seen on the page yet. C3.10 |
| D2 | Summary box, Live (You, The rules alone, index fund) | §6 Strategy page | smoke | 2026-11-06-1 | C3.10 |
| D3 | Stock rows: pills, detail lines, order, per template | §6 Strategy page | smoke | 2026-11-06-1 | Before any daily check: L-002. F-006, F-007 |
| D4 | Edit list on a running strategy | §6 Strategy page | smoke | 2026-11-06-1 | C3.11 |
| D5 | Signals and trades timeline | §6 Strategy page | smoke | 2026-11-06-1 | F-005 |
| D6 | Log a trade by hand (live) | §6 Strategy page | smoke | 2026-11-06-1 | F-001. C3.5–7 |
| D7 | Edit and delete a trade (live) | §6 Strategy page | smoke | 2026-11-06-1 | C3.9 |
| D8 | Rename | §6 Strategy page | smoke | 2026-11-06-1 | C3.4 |
| D9 | Tune the settings while running | §6 Strategy page | smoke | 2026-11-06-1 | Not saved with a change: L-008. C3.12 |
| D10 | Change the budget while running | §6 Strategy page | untested | | Not built yet (ROADMAP) |
| D11 | Go live from a simulation | §4 Going live | smoke | 2026-11-06-1 | C2.15 |
| D12 | Archive, then delete | §6 Strategy page | smoke | 2026-11-06-1 | Throwaways only. C3.15 |
| D13 | An archived strategy is read-only | §6 Strategy page | smoke | 2026-11-06-1 | C3.16 |
| D14 | Metrics arithmetic: budget used, fees, gains, vs the index | §6 Metrics, §5 Modes | smoke | 2026-11-06-1 | Sums by hand match. F-001, F-005. C2.6, C2.14, C3.5–6 |

### E · Stock page

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| E1 | Status box, position, its signals and trades | §6 Stock page | smoke | 2026-11-06-1 | C2.18, C3.17 |
| E2 | Rules on [date]: badges and gauges, per template | §6 Stock page | untested | | Needs a daily check: L-002 |
| E3 | A stock not in the strategy, a wrong ID | §6 Stock page | smoke | 2026-11-06-1 | C3.18 |
| E4 | Price chart: 90/30/7 days, each template's levels day by day, candidate lines only before the start, Started marker, trade dots, candidate vs not colours | §6 Stock page | untested | | |

### F · Confirm a signal (live)

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| F1 | I did it: fields, prefill, shares instead, validation | §6 Signal | smoke | 2026-11-06-1 | F-003. Prefilled price needs a stored open. C2.13 |
| F2 | I skipped it: reasons | §6 Signal | smoke | 2026-11-06-1 | C2.16 |
| F3 | Snooze | §6 Signal | smoke | 2026-11-06-1 | L-001. C2.17 |
| F4 | Answered and missed signals | §6 Signal | smoke | 2026-11-06-1 | Answered only; missed: L-001. C2.14 |
| F5 | Exchange-rate line (stock currency ≠ strategy currency) | §6 Signal | smoke | 2026-11-06-1 | F-004. C2.12 |
| F6 | Sell signal (Index trend) | §6 Signal | untested | | Needs a sell signal on a live fixture |

### G · AI strategies

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| G1 | AI round, Simulation: copy, paste, scores, Use these scores | §6 AI round | smoke | 2026-11-06-1 | The agent writes the answer; decline sharing. C2.1, C2.5 |
| G2 | AI round, Live: opens Confirm a signal | §6 AI round | smoke | 2026-11-06-1 | C2.11 |
| G3 | The answer reader: tables, JSON, broken answers, the error box | §6 AI round | deep | 2026-11-06-1 | C2.2–4 |
| G4 | Extra round, skip a round, before it's due | §6 AI round | smoke | 2026-11-06-1 | Skip a round not pressed. C2.9, C2.17 |
| G5 | AI rounds history, Picks vs the rest | §6 AI rounds | smoke | 2026-11-06-1 | F-020. C2.10 |
| G6 | The strategy's prompt editor | §6 AI | untested | 2026-11-06-1 | Read only: L-008. C2.19 |
| G7 | AI strategy page and stock rows (WATCHING, last score) | §6 AI strategies | smoke | 2026-11-06-1 | F-006, F-007. C2.7–8 |

### H · Account and feedback

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| H1 | Account page: email, install the app | §6 Account | smoke | 2026-11-06-1 | C5.5 |
| H2 | Notifications: boxes, This device | §6 Account, §7 | smoke | 2026-11-06-1 | Summary box only; push not turned on. Restore the baseline. C5.4 |
| H3 | Currency | §6 Account | smoke | 2026-11-06-1 | Restore the baseline. C5.3 |
| H4 | Language (and the whole app in Catalan) | §6 Account | smoke | 2026-11-06-1 | Restore the baseline. C5.1 |
| H5 | In-app feedback: select text, 💬 Add comment | §6 Admin, Feedback | smoke | 2026-11-06-1 | Send feedback only; select text: L-007. Once a session, `QA test, ignore:`. C5.6 |
| H6 | `/feedback` page | §6 Admin, Feedback | smoke | 2026-11-06-1 | Refused for a vip, as it should be. C5.7 |
| H7 | Unsubscribe page (an invalid token) | §7 | smoke | 2026-11-06-1 | Don't use a real token. C5.8 |

### X · Across the app

| ID | Area | Depth | Last | Notes |
| --- | --- | --- | --- | --- |
| X1 | Product rules sweep (GUIDE, *Oracles*) on every screen | smoke | 2026-11-06-1 | F-009, F-021. C5.10 |
| X2 | Catalan on every screen | smoke | 2026-11-06-1 | F-011 to F-016. C5.2 |
| X3 | 390px, 360px and desktop on every screen; long names | smoke | 2026-11-06-1 | Guest screens only: L-005. F-012 |
| X4 | Top bar: DASHBOARD / WELCOME home link, ← up links, the right side's ACCOUNT / LOG IN / ADMIN, browser Back, direct addresses (§4) | untested | | Reworked 2026-11-08 (home and up links replace history back). Check the arrow sits centred on iPhone |
| X5 | Reloads, two tabs, a stale tab, double submits | smoke | 2026-11-06-1 | Double submit and reload only: L-007 |
| X6 | Access control: other IDs, `/admin` as a vip, signed out | smoke | 2026-11-06-1 | Signed out: L-009. Only through the address bar. C5.7, C5.9 |
| X7 | Accessibility: keyboard, labels, focus, targets, contrast | untested | 2026-11-06-1 | Spot checks only. F-018. C5.12 |
| X8 | Console errors, slow pages, the PWA (install, offline) | smoke | 2026-11-06-1 | Offline: L-007. C5.11 |
| X9 | Results consistency: dashboard vs strategy vs stock vs signal | smoke | 2026-11-06-1 | Agree so far. F-005, F-020. C3.8 |
