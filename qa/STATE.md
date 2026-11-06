# QA state

What the next session reads first. The agent updates it at the end of every session. Ernest may edit anything here, for example to point the next session at an area.

## Handoff

*Last session:* none yet.

**Next session:** the first one, a breadth session (see GUIDE, *Choose the charters*). Set up the fixtures, record the account baseline, and take every row of groups A to H to **smoke**. Note what the long-running strategies look like today, so later sessions can compare.

## Account baseline

**Account:** `qaagent` (README, *The account's label*). Another account gets its own copy of this section and of *Fixtures*, under its label.

The account's settings before any session changed them. Restore them before ending. The first session fills this in.

| Setting | Value |
| --- | --- |
| Language | |
| Currency | |
| Signals and reminders: App / Email | |
| Summary: Email | |
| This device (push) | |
| Strategies not starting with `QA ` (leave alone) | |

## Fixtures

Long-running `QA` strategies that stay from session to session, so that what happens over days (daily checks, signals, statuses, missed signals, AI rounds due, results) can be tested. Never archive them. At most 6, so there is room to create throwaway ones under the 10-running cap. The first session creates them with the default settings and starter lists unless noted.

| Name | Template | Mode | Created | Notes |
| --- | --- | --- | --- | --- |
| QA fixture · Discount · Sim | Winners at a discount | Simulation | | |
| QA fixture · Comeback · Live | The comeback | Live | | |
| QA fixture · Bear · Sim | Bear markets | Simulation | | |
| QA fixture · Index · Live | Index trend | Live | | |
| QA fixture · AI · Sim | Your AI's top picks | Simulation | | AI round every 1 day, so one is due each session |
| QA fixture · AI · Live | Your AI's top picks | Live | | AI round every 1 day |

## Lead queue

Open leads to follow, most important first. **next** means the next session takes it before new charters. When a lead is done, delete it here (the log keeps its history).

| ID | Area | What to try | From | Priority |
| --- | --- | --- | --- | --- |
| | | | | |

## Coverage map

Depth: **untested** → **smoke** (happy path seen and checked against the spec) → **deep** (variations, edge cases and the oracles done, its leads followed) → **regressed** (re-checked after a fix or a change to the area). A row drops back to **smoke** when the area changes (a merged PR).

Areas come from the spec ([§4–7](../docs/strategy-trading-lab-app-spec.md)). Add a row when a feature ships; mark one **removed** when it goes.

### A · Public and sign-in

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| A1 | Welcome (`/`, `/welcome`), guest, its preview widgets | §6 Welcome | untested | | Needs signing out, or a private window |
| A2 | Guest new-strategy flow (`/new` without an account) | §6 Looking around | untested | | |
| A3 | Sign-up form: validation, invite code lookup | §6 Sign-up | untested | | Never submit it (README) |
| A4 | Log-in, log-out, coming back to the page asked for | §6 Log-in | untested | | Wrong password only with `qa-nobody@stl.invalid` |
| A5 | Forgot password | §6 Forgot | untested | | Only `qa-nobody@stl.invalid` |
| A6 | Legal pages, the footer, the site map | §6, CLAUDE.md | untested | | |
| A7 | Language links (`/ca/…`), unknown addresses | §4 | untested | | |

### B · New strategy flow

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| B1 | Pick a template (count of strategies already from it) | §6.1 | untested | | |
| B2a | Review: Winners at a discount | §6.2 | untested | | |
| B2b | Review: The comeback | §6.2 | untested | | |
| B2c | Review: Bear markets (fund picker) | §6.2 | untested | | |
| B2d | Review: Index trend | §6.2 | untested | | |
| B2e | Review: Your AI's top picks (sources, prompt) | §6.2 | untested | | |
| B3 | Stock list on the review: tick, untick, + Add a stock | §6.2 | untested | | Adding a new stock writes to shared assets: only a well-known symbol |
| B4 | Advanced settings, per template | §6.2b | untested | | |
| B5 | How we tested | §6.2c | untested | | |
| B6 | Prompt editor (AI draft) | §6.2 | untested | | |
| B7 | Choose the mode (Live needs a confirmed email) | §6.3 | untested | | |
| B8 | Set your budget: budget, step, max steps, currency | §6.4 | untested | | |
| B9 | Name, preview and confirm: name rules, Edit links, notifications ask | §6.5 | untested | | |
| B10 | The draft across reloads, Back, and leaving and coming back | §4 | untested | | |

### C · Dashboard

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| C1 | Strategy cards: result, index fund, worst year, chart, budget bar | §6 Dashboard | untested | | |
| C2 | Waiting for you: live signals, AI rounds due | §6 Dashboard | untested | | |
| C3 | The cap of 10 running strategies | §6 Dashboard | untested | | Reach it with throwaways, then clean up |
| C4 | Archived section | §6 Dashboard | untested | | |

### D · Strategy page

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| D1 | Summary box, Simulation | §6 Strategy page | untested | | |
| D2 | Summary box, Live (You, The rules alone, index fund) | §6 Strategy page | untested | | |
| D3 | Stock rows: pills, detail lines, order, per template | §6 Strategy page | untested | | |
| D4 | Edit list on a running strategy | §6 Strategy page | untested | | |
| D5 | Signals and trades timeline | §6 Strategy page | untested | | |
| D6 | Log a trade by hand (live) | §6 Strategy page | untested | | |
| D7 | Edit and delete a trade (live) | §6 Strategy page | untested | | |
| D8 | Rename | §6 Strategy page | untested | | |
| D9 | Tune the settings while running | §6 Strategy page | untested | | |
| D10 | Change the budget while running | §6 Strategy page | untested | | |
| D11 | Go live from a simulation | §4 Going live | untested | | |
| D12 | Archive, then delete | §6 Strategy page | untested | | Throwaways only |
| D13 | An archived strategy is read-only | §6 Strategy page | untested | | |
| D14 | Metrics arithmetic: budget used, fees, gains, vs the index | §6 Metrics, §5 Modes | untested | | High value: S1s live here |

### E · Stock page

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| E1 | Status box, position, its signals and trades | §6 Stock page | untested | | |
| E2 | Rules on [date]: badges and gauges, per template | §6 Stock page | untested | | |
| E3 | A stock not in the strategy, a wrong ID | §6 Stock page | untested | | |

### F · Confirm a signal (live)

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| F1 | I did it: fields, prefill, shares instead, validation | §6 Signal | untested | | Needs a waiting signal on a live fixture |
| F2 | I skipped it: reasons | §6 Signal | untested | | |
| F3 | Snooze | §6 Signal | untested | | |
| F4 | Answered and missed signals | §6 Signal | untested | | |
| F5 | Exchange-rate line (stock currency ≠ strategy currency) | §6 Signal | untested | | |
| F6 | Sell signal (Index trend) | §6 Signal | untested | | |

### G · AI strategies

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| G1 | AI round, Simulation: copy, paste, scores, Use these scores | §6 AI round | untested | | The agent writes the answer; decline sharing |
| G2 | AI round, Live: opens Confirm a signal | §6 AI round | untested | | |
| G3 | The answer reader: tables, JSON, broken answers, the error box | §6 AI round | untested | | |
| G4 | Extra round, skip a round, before it's due | §6 AI round | untested | | |
| G5 | AI rounds history, Picks vs the rest | §6 AI rounds | untested | | |
| G6 | The strategy's prompt editor | §6 AI | untested | | |
| G7 | AI strategy page and stock rows (WATCHING, last score) | §6 AI strategies | untested | | |

### H · Account and feedback

| ID | Area | Spec | Depth | Last | Notes |
| --- | --- | --- | --- | --- | --- |
| H1 | Account page: email, install the app | §6 Account | untested | | |
| H2 | Notifications: boxes, This device | §6 Account, §7 | untested | | Restore the baseline |
| H3 | Currency | §6 Account | untested | | Restore the baseline |
| H4 | Language (and the whole app in Catalan) | §6 Account | untested | | Restore the baseline |
| H5 | In-app feedback: select text, 💬 Add comment | §6 Admin, Feedback | untested | | Once a session, `QA test, ignore:` |
| H6 | `/feedback` page | §6 Admin, Feedback | untested | | |
| H7 | Unsubscribe page (an invalid token) | §7 | untested | | Don't use a real token |

### X · Across the app

| ID | Area | Depth | Last | Notes |
| --- | --- | --- | --- | --- |
| X1 | Product rules sweep (GUIDE, *Oracles*) on every screen | untested | | |
| X2 | Catalan on every screen | untested | | |
| X3 | 390px, 360px and desktop on every screen; long names | untested | | |
| X4 | Back and up: ← links, browser Back, direct addresses (§4) | untested | | |
| X5 | Reloads, two tabs, a stale tab, double submits | untested | | |
| X6 | Access control: other IDs, `/admin` as a vip, signed out | untested | | Only through the address bar |
| X7 | Accessibility: keyboard, labels, focus, targets, contrast | untested | | |
| X8 | Console errors, slow pages, the PWA (install, offline) | untested | | |
| X9 | Results consistency: dashboard vs strategy vs stock vs signal | untested | | |
