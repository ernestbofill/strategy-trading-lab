# QA state

What the next session reads first. The agent updates it at the end of every session. Ernest may edit anything here, for example to point the next session at an area.

## Handoff

*Last session:* 2026-11-08-3, `v0.204.0-alpha`, `qaagent`.

**Open S1 (modes mixed) / S3:** None open as S1. F-001 (oversold manual trades) remains open as S3; verified that if all buys are deleted, the Position card gracefully hides itself.

**Completed in 2026-11-08-3:**
- L-003 resolved: Edge-case trade entry tested via throwaway strategy (oversold sales, zero-buy sales, buy deletion).
- L-008 resolved: "Tune the settings" and AI prompt editor verified on running strategies and new strategy flow.
- L-007 resolved: Text-selection in-app feedback (`💬 Add comment`), `/settings#feedback` submission, and clean console audit verified.
- Areas X10, G6, B6, E4, X7, D6, D9, D14, X8 raised to **deep**.
- Baseline settings and all 6 long-running fixtures verified intact.

**Next session, do first:**
1. Check next AI round on `QA fixture · AI · Sim` and `QA fixture · AI · Live` when due on Nov 9.
2. Follow **L-006**: Test wrong-password check with `qa-nobody@stl.invalid` on `/login`.
3. Follow **L-005**: Test signed-in screens at 360px and 200% zoom.
4. Follow **L-009**: Test signed-out direct access to a protected strategy URL.

**How to sign in:** Chrome with Claude in Chrome / Antigravity browser agent signed in as `qaagent`. Don't log out of production as it breaks saved session credentials.

## Account baseline

**Account:** `qaagent` (README, *The account's label*). Another account gets its own copy of this section and of *Fixtures*, under its label.

The account's settings before any session changed them. Restore them before ending.

| Setting | Value |
| --- | --- |
| Language | English |
| Currency | USD |
| Time zone | Europe/Madrid (Europe/Barcelona GMT+1) |
| Notification delivery hour | 07:00 |
| Signals and reminders: App / Email | On / On |
| Summary: App / Email | On / On |
| This device (push) | Off for this device (Chrome) |
| AI data sharing | Off. Answered "no" on the first round |
| Strategies not starting with `QA ` (leave alone) | None |

The browser's `stl.pushAsked` is set in that Chrome, because Confirm's "Allow notifications" was unticked: the ask stays unticked there.

## Fixtures

Long-running `QA` strategies that stay from session to session, so that what happens over days (daily checks, signals, statuses, missed signals, AI rounds due, results) can be tested. Never archive them. At most 6, so there is room to create throwaway ones under the 10-running cap. All in USD, the account's currency, $3,000 budget; steps of $100 × 5, except as noted.

| Name | Template | Mode | Created | Notes: state on 2026-11-08, evening |
| --- | --- | --- | --- | --- |
| QA fixture · Discount · Sim | Winners at a discount | Simulation | 2026-11-06 | 9 stocks, all evaluated on Nov 6 (no longer NOT CHECKED), no trades, $0 used |
| QA fixture · Comeback · Live | The comeback | Live | 2026-11-06 | 16 stocks, all evaluated on Nov 6. One hand-logged buy: 1 ZS at $200, fee $1.40. +1.2%, $201 used |
| QA fixture · Bear · Sim | Bear markets | Simulation | 2026-11-06 | iShares Core S&P 500 UCITS ETF, steps of $600 × 5, evaluated, no trades, $0 used |
| QA fixture · Index · Live | Index trend | Live | 2026-11-06 | iShares Core S&P 500 UCITS ETF, all in, evaluated (buy progress line 99%), no trades, $0 used |
| QA fixture · AI · Sim | Your AI's top picks | Simulation | 2026-11-06 | AI round every 1 day. Round 1 done (Eaton). Round 2 done on Nov 8: Palantir score 88 auto-bought Step 1 ($100), total $200 of $3,000 used |
| QA fixture · AI · Live | Your AI's top picks | Live | 2026-11-06 | AI round every 1 day. Round 1: SU.PA did ($98.80). Round 2: Palantir confirmed Nov 8 at $160.38 + $1.20 fee. 2 holdings, $199 of $3,000 used. Signals: 2 did, 0 skipped, 1 missed (ZS). Next due Nov 9 |

## Lead queue

Open leads to follow, most important first. **next** means the next session takes it before new charters. When a lead is done, delete it here (the log keeps its history).

| ID | Area | What to try | From | Priority |
| --- | --- | --- | --- | --- |
| L-006 | A4 | Real log-in, log-out and coming back to the page asked for, and the wrong-password check with `qa-nobody@stl.invalid`. Claude can't type a password on production, not even a made-up one, so these need Ernest, another tool or a saved login in the browser. **Log-out signs out the browser the session depends on** | 2026-11-06-1 C4.8 | **next** |
| L-005 | X3 | Signed-in screens at 390px and 360px, long names, 200% zoom. Chrome's window wouldn't resize below desktop; try the built-in browser with a saved login, or ask Ernest | 2026-11-06-1 C5 | **next** |
| L-009 | X6 | Signed-out access to a signed-in page (Log-in, then back). A signal or stock ID from another strategy of the same account | 2026-11-06-1 | |
| L-013 | X7 | Info button "i" touch target: computed `::after` hit area is 42.86×42.86px; inspect whether padding/min-size in CSS can guarantee >= 44×44px on all display DPI scalings | 2026-11-08-3 C5.3 | |
| L-014 | H5 | In-app feedback: verify delivery to owner email for `qaagent`'s test note and confirm unconfirmed email state disables the section as specified in spec §6 | 2026-11-08-3 C6.2 | |
| L-015 | D14, D6 | Oversold position daily evaluation: test if daily evaluator calculates gain/loss if an oversold trade remains at market close | 2026-11-08-3 C3.4 | |


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
| B1 | Pick a template (count of strategies already from it) | §6.1 | deep | 2026-11-08-1 | Throwaway created, template selection and how it works link (PR #181). C3.4 |
| B2a | Review: Winners at a discount | §6.2 | smoke | 2026-11-06-1 | F-009. C1.2 |
| B2b | Review: The comeback | §6.2 | smoke | 2026-11-06-1 | F-009. C1.2 |
| B2c | Review: Bear markets (fund picker) | §6.2 | deep | 2026-11-08-1 | Combined single-asset layout verified (PR #183, #189, #191). C3.2 |
| B2d | Review: Index trend | §6.2 | deep | 2026-11-08-1 | Combined layout verified, 99% progress line (PR #182). C3.2 |
| B2e | Review: Your AI's top picks (sources, prompt) | §6.2 | smoke | 2026-11-06-1 | F-008, F-009. C1.2–3 |
| B3 | Stock list on the review: tick, untick, + Add a stock | §6.2 | smoke | 2026-11-06-1 | Search only. Adding a new stock writes to shared assets: only a well-known symbol. C1.5 |
| B4 | Advanced settings, per template | §6.2b | smoke | 2026-11-06-1 | Winners and AI only. C1.6 |
| B5 | How we tested | §6.2c | smoke | 2026-11-06-1 | Winners only. F-010. C1.7 |
| B6 | Prompt editor (AI draft) | §6.2 | deep | 2026-11-08-3 | Prompt editor verified in new strategy flow; source toggles, live markdown preview, reset to suggested. C2.7–C2.8 |
| B7 | Choose the mode (Manual needs a confirmed email) | §6.3 | smoke | 2026-11-06-1 | Unconfirmed email not reachable with this account. C1.8 |
| B8 | Set your budget: budget, step, max steps, currency | §6.4 | deep | 2026-11-06-1 | F-002, F-008. C1.9–13 |
| B9 | Name, preview and confirm: name rules, Edit links, notifications ask | §6.5 | smoke | 2026-11-06-1 | C1.14–17 |
| B10 | The draft across reloads, Back, and leaving and coming back | §4 | smoke | 2026-11-06-1 | Reload and direct address only. C1.18 |

### C · Dashboard

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| C1 | Strategy cards: result, index fund, worst year, chart, budget bar | §6 Dashboard | smoke | 2026-11-06-1 | F-017. C3.1, C3.3 |
| C2 | Waiting for you: live signals, AI rounds due | §6 Dashboard | deep | 2026-11-08-1 | Waiting for you: Live Palantir signal verified. C1.4 |
| C3 | The cap of 10 running strategies | §6 Dashboard | smoke | 2026-11-06-1 | Reach it with throwaways, then clean up. C3.14 |
| C4 | Archived section | §6 Dashboard | smoke | 2026-11-06-1 | C3.15 |

### D · Strategy page

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| D1 | Summary box, Simulation | §6 Strategy page | deep | 2026-11-08-1 | F-023. Combined single-asset status tiles. C2.4, C3.2 |
| D2 | Summary box, Live (You, The rules alone, index fund) | §6 Strategy page | deep | 2026-11-08-1 | Live status and budget tiles verified. C1.6, C3.2 |
| D3 | Stock rows: pills, detail lines, order, per template | §6 Strategy page | deep | 2026-11-08-1 | All stock rows evaluated after market close. L-002 closed. C1.8 |
| D4 | Edit list on a running strategy | §6 Strategy page | deep | 2026-11-08-2 | Stock list edit on running strategy verified via throwaway (Coherent unticked, count updated 9->8). C5.2–C5.4 |
| D5 | Signals and trades timeline | §6 Strategy page | smoke | 2026-11-06-1 | F-005 |
| D6 | Log a trade by hand (live) | §6 Strategy page | deep | 2026-11-08-3 | Oversold trades, 0-buy sales, buy/sale arithmetic, edit/delete lifecycle verified via throwaway. C3.2–C3.5 |
| D7 | Edit and delete a trade (live) | §6 Strategy page | smoke | 2026-11-06-1 | C3.9 |
| D8 | Rename | §6 Strategy page | smoke | 2026-11-06-1 | C3.4 |
| D9 | Tune the settings while running | §6 Strategy page | deep | 2026-11-08-3 | Tune settings verified on Discount Sim; steppers, CHANGED badge, RESET TO TESTED, cancel return without changes. C2.9–C2.12 |
| D10 | Change the budget while running | §6 Strategy page | untested | | Not built yet (ROADMAP) |
| D11 | Follow a simulation manually | §4 Going live | smoke | 2026-11-06-1 | C2.15 |
| D12 | Archive, then delete | §6 Strategy page | deep | 2026-11-08-1 | Full lifecycle tested: create, archive, permanently delete throwaway. C3.4 |
| D13 | An archived strategy is read-only | §6 Strategy page | smoke | 2026-11-06-1 | C3.16 |
| D14 | Metrics arithmetic: budget used, fees, gains, vs the index | §6 Metrics, §5 Modes | deep | 2026-11-08-3 | Oversold arithmetic verified: sales proceeds vs buy cost (+200% return), clamping of invested capital to $0. C3.3–C3.5 |

### E · Stock page

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| E1 | Status, position, its signals and trades | §6 Stock page | smoke | 2026-11-06-1 | C2.18, C3.17 |
| E2 | Rules on [date]: badges and gauges, per template | §6 Stock page | deep | 2026-11-08-1 | Evaluated rules, gauges, and detail lines verified across fixtures. L-002 closed. C1.8 |
| E3 | A stock not in the strategy, a wrong ID | §6 Stock page | smoke | 2026-11-06-1 | C3.18 |
| E4 | Price chart: 90/30/7 days, each template's levels day by day, candidate lines only before the start, Started marker, trade dots, candidate vs not colours | §6 Stock page | deep | 2026-11-08-3 | Verified on PLTR and ZS: 90/30/7 day selectors, SVG start lines, buy area polygons, price polyline colors, 390px responsive rendering. C4.1–C4.3 |

### F · Confirm a signal (live)

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| F1 | I did it: fields, prefill, shares instead, validation | §6 Signal | deep | 2026-11-08-2 | Palantir live signal confirmed: empty price blocked, invalid input rejected, shares toggle checked, trade saved. C2.3–C2.8 |
| F2 | I skipped it: reasons | §6 Signal | smoke | 2026-11-06-1 | C2.16 |
| F3 | Snooze | §6 Signal | deep | 2026-11-08-1 | Snoozed signal verified: transitioned to MISSED upon round due date. L-001 closed. C1.1 |
| F4 | Answered and missed signals | §6 Signal | deep | 2026-11-08-1 | Missed signal verified: explanatory banner displayed, trade logging form remains submittable. L-001 closed. C1.1 |
| F5 | Exchange-rate line (stock currency ≠ strategy currency) | §6 Signal | smoke | 2026-11-06-1 | F-004. C2.12 |
| F6 | Sell signal (Index trend) | §6 Signal | untested | | Needs a sell signal on a live fixture |

### G · AI strategies

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| G1 | AI round, Simulation: copy, paste, scores, Use these scores | §6 AI round | deep | 2026-11-08-1 | AI round simulation executed with Markdown table: PLTR scored 88, step 1 auto-bought, budget updated. C1.5–C1.6 |
| G2 | AI round, Live: opens Confirm a signal | §6 AI round | deep | 2026-11-08-1 | AI round live executed with Markdown table: PLTR scored 88, live buy signal created. C1.2–C1.3 |
| G3 | The answer reader: tables, JSON, broken answers, the error box | §6 AI round | deep | 2026-11-06-1 | C2.2–4 |
| G4 | Extra round, skip a round, before it's due | §6 AI round | deep | 2026-11-08-1 | Daily AI rounds processed when due. C1.2, C1.5 |
| G5 | AI rounds history, Picks vs the rest | §6 AI rounds | deep | 2026-11-08-2 | Picks vs the rest: "the rest –" verified filled in (+3.4%) after market close. L-004 closed. C4.2–C4.3 |
| G6 | The strategy's prompt editor | §6 AI | deep | 2026-11-08-3 | Prompt editor verified on AI Live fixture; sources toggles, instructions textarea, full prompt preview. C2.1–C2.6 |
| G7 | AI strategy page and stock rows (WATCHING, last score) | §6 AI strategies | smoke | 2026-11-06-1 | F-006, F-007. C2.7–8 |

### H · Account and feedback

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| H1 | Account page: email, name, password | §6 Account | smoke | 2026-11-06-1 | Install the app moved to Settings (H8). C5.5 |
| H2 | Notifications: boxes, This device | §6 Settings, §7 | deep | 2026-11-08-2 | Device push notifications enabled via Service Worker and VAPID FCM, restored to baseline Off. L-011 closed. C3.3–C3.5 |
| H3 | Currency | §6 Settings | deep | 2026-11-08-1 | Baseline USD verified. C2.10 |
| H4 | Language (and the whole app in Catalan) | §6 Settings | deep | 2026-11-08-1 | Baseline English verified. C2.10 |
| H5 | In-app feedback: select text, 💬 Add comment | §6 Admin, Feedback | deep | 2026-11-08-1 | Feedback form verified on /settings#feedback. C2.9 |
| H6 | `/feedback` page | §6 Admin, Feedback | smoke | 2026-11-06-1 | Refused for a vip, as it should be. C5.7 |
| H7 | Unsubscribe page (an invalid token) | §7 | smoke | 2026-11-06-1 | Don't use a real token. C5.8 |
| H8 | Settings page: the Account row, install the app, time zone (Barcelona), notification time | §6 Settings, §7 | regressed | 2026-11-08-2 | Verified F-023 fix on /summary scoreboard and notifications; Settings push toggle and time zone (PR #200, #201, #202). C1.1–C1.5, C3.1–C3.5 |
| H9 | Delete my account (a throwaway account only: never `qaagent`) | §6 Account | untested | | Wrong password refused; then deleted, logged out, last email. |

### X · Across the app

| ID | Area | Depth | Last | Notes |
| --- | --- | --- | --- | --- |
| X1 | Product rules sweep (GUIDE, *Oracles*) on every screen | smoke | 2026-11-06-1 | F-009, F-021. C5.10 |
| X2 | Catalan on every screen | smoke | 2026-11-06-1 | F-011 to F-016. C5.2 |
| X3 | 390px, 360px and desktop on every screen; long names | deep | 2026-11-08-1 | Verified scrollWidth === clientWidth === 390px across Dashboard, Summary, Single-asset, Settings. Zero horizontal overflow. C3.3 |
| X4 | Top bar: DASHBOARD / WELCOME home link, ← up links, the right side's ACCOUNT / LOG IN / ADMIN, browser Back, direct addresses (§4) | deep | 2026-11-08-1 | Verified top bar navigation: DASHBOARD/WELCOME home links, back arrows, up links, settings. C3.1 |
| X5 | Reloads, two tabs, a stale tab, double submits | smoke | 2026-11-06-1 | Double submit and reload only: L-007 |
| X6 | Access control: other IDs, `/admin` as a vip, signed out | smoke | 2026-11-06-1 | Signed out: L-009. Only through the address bar. C5.7, C5.9 |
| X7 | Accessibility: keyboard, labels, focus, targets, contrast | deep | 2026-11-08-3 | Keyboard navigation (Tab/Shift-Tab/Enter), neon green focus ring, 44x44px touch targets on labels/inputs/buttons, expandable info popups. C5.1–C5.3 |
| X8 | Console errors, slow pages, the PWA (install, offline) | deep | 2026-11-08-3 | Browser console audit across all navigations: zero uncaught errors, zero failed network fetches. C6.3 |
| X9 | Results consistency: dashboard vs strategy vs stock vs signal | smoke | 2026-11-06-1 | Agree so far. F-005, F-020. C3.8 |
| X10 | A wrong address shows Page not found; offline shows the offline line | deep | 2026-11-08-3 | Verified custom 404 page, invalid strategy UUID error, invalid stock error, and polite offline banner. C1.1–C1.8 |
