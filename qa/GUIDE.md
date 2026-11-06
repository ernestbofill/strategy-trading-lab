# How to run a QA session

You are an expert manual tester exploring Strategy Trading Lab on production. Your job is to find what is wrong, confusing or inconsistent, as a sceptical user and a QA engineer would, and to leave a record that the next session can build on.

Read [README.md](README.md) (safety rules) and [STATE.md](STATE.md) first. Keep the spec ([docs/strategy-trading-lab-app-spec.md](../docs/strategy-trading-lab-app-spec.md)) open as your reference: it says what every screen should show and do.

## Why this guide is strict about stopping

AI testers stop too early. They find one bug and report it, they see a happy path work and call the area "fine", or they run out of ideas and wrap up while there is budget left. An expert human keeps pulling the thread: *if this is off, what else is?* This guide replaces that instinct with rules you follow:

1. **A finding is a lead, not an ending.** Every finding and every oddity opens follow-up questions (*Following a lead*, below). You answer them in the log.
2. **Charters end when their budget does,** not when you find something or feel done.
3. **Coverage only goes up with evidence.** You may raise an area's depth only by writing down what you did and saw.
4. **You may only stop after the stop checklist,** which asks for things you didn't try. If you can't name them, you haven't finished looking.

"Looks fine" is never a result. Write what you checked and what you saw.

## The session, step by step

### 1. Prepare (about 5 actions)

1. Read `STATE.md`: the handoff note, the lead queue, the coverage map, the fixtures and the account baseline.
2. Create the session log `sessions/YYYY-MM-DD-N.md` from [_TEMPLATE.md](sessions/_TEMPLATE.md), on a new branch `qa/YYYY-MM-DD-N` from the latest `main`.
3. Open the app. Note the version at the end of the footer and the date. Note what changed since the last session: the PRs merged since its date (`git log --since`). **Changed areas get a charter first:** new code is where the new bugs are.
4. Check you're signed in as the session's account (the command's argument; ask Ernest if none was given). If not, sign in with the browser's saved login for it (README).
5. Write the session's plan in the log: 3 to 5 charters (below).

### 2. Choose the charters

A charter is a one-line mission: **Explore** *[area]* **with** *[resources or technique]* **to discover** *[kind of problem]*.

> Explore Set your budget with odd amounts, step counts and currencies to discover wrong totals or missing validation.

Pick them in this order, until you have 3 to 5:

1. The open leads in `STATE.md`'s queue marked **next**. A lead counts as one charter or part of one.
2. Areas changed since the last session (step 1.3).
3. Areas at depth **untested**, then **smoke**, in the coverage map.
4. Areas tested longest ago.
5. One **cross-cutting** charter each session (rows X in the map): a sweep of one oracle across many screens, for example Catalan everywhere, or every screen at 390px.

The first session (no rows above **untested**) is a **breadth session**: one charter per group (A to H) at smoke depth, plus setting up the fixtures.

### 3. Run each charter

**Budget: about 50 browser actions per charter** (a click, a typed field, a navigation, a resize or a reload is one action). Write the count in the log.

Inside a charter, cycle until the budget runs out:

1. **Happy path once.** Do what a normal user would. Write what you saw, checked against the spec.
2. **Vary it.** Use the techniques below, one at a time. Each variation is a log line: what you did, what you expected, what happened.
3. **Apply the oracles** (below) to every screen you reach.
4. **On anything odd, follow the lead** (below) before going on.

If the area is genuinely exhausted before the budget is spent, don't stop: widen the charter to a neighbouring area (the screen before or after it in the flow), and say so in the log. A charter ends early only when it is **blocked** (a bug stops the flow, or a safety rule forbids the next step). Write the reason.

### 4. Following a lead

Whenever you find a bug, or something only looks slightly off (a number you can't explain, a flash of wrong text, a slow load, a different wording than elsewhere), stop and write a **lead block** in the log:

1. **Reproduce.** Do it again. Does it happen every time? Write the shortest steps.
2. **Where else?** The same component, pattern or rule on other screens, the other mode (Simulation/Live), the other templates, Catalan, 390px vs desktop.
3. **Neighbours.** The step before and after it in the flow. Try Back, reload, opening the address directly, a second tab.
4. **Variations.** Empty, zero, the smallest and largest allowed, one past the limit, negative, decimals with a comma, very long text, accents and emoji, pasting, double tap.
5. **Root guess.** Only the screen, or the data? Does it survive a reload? Does another screen showing the same data agree (dashboard card vs strategy page vs stock page)?
6. **Severity.** Could it cost a user money or trust, mix the two modes, show wrong results, or break a product rule? (The scale is below.)

Try each question right away while the charter has budget. When you can't, add it to the lead queue in `STATE.md` with what is left to try, and mark it **next** when it might be serious. A lead that turns up a second bug starts its own lead block. Go three levels deep before moving on.

### 5. Record findings

A confirmed problem goes into [FINDINGS.md](FINDINGS.md) with the next free ID (`F-001` and up), severity, area ID, steps, expected (with the spec section), actual, and how often it happens. When it's already there, add the session to it instead of a new entry. Re-check open findings in the areas you test, and mark them **fixed** when they no longer happen.

**Severity:**

- **S1:** wrong money or results, the two modes mixed, data lost or shown to the wrong user, a security problem, a product rule broken in a way that misleads (an instruction to buy, a missing disclaimer).
- **S2:** a feature doesn't work or blocks the flow, with no way round it.
- **S3:** works, but wrong: copy that differs from the spec, layout breaks, the wrong state shown, poor accessibility, untranslated text.
- **S4:** a nit, a polish idea or an improvement. Ideas are welcome: mark them **idea**.

An S1 also goes at the top of the session's handoff, and in the chat when the session ends.

### 6. Stop checklist

You may end the session only when the log has all of these, written out:

- [ ] Each charter: its budget used, what it covered, and why it ended (budget, or blocked by …).
- [ ] The coverage rows whose depth went up, with the log lines that are the evidence.
- [ ] **Three or more things you could have tried and didn't**, each added to the lead queue.
- [ ] Every open lead from this session is in the queue, with what is left to try.
- [ ] Throwaway `QA` strategies archived and deleted; fixtures still running; the account's settings back to the baseline.
- [ ] `STATE.md` updated: the coverage map (depth, date, session), the lead queue, the handoff note for the next session (what to do first, and why).

If any is missing, go back and do it. If budget is left (fewer than 3 charters ran), run another charter instead of stopping.

Then commit, push and open the PR (README, *After a session*), and give Ernest a short summary in the chat: charters, new findings by severity, S1s in full, and the PR link.

## Browser-only mode

For an agent that can browse but can't write to the repo (Gemini in Chrome, or any tool without file access). Everything above applies, with these changes:

- **Read** `README.md`, `GUIDE.md` and `STATE.md` on GitHub (the links in README, *From Gemini in Chrome*), not from disk. Skip creating a branch and a file.
- **Shorter session:** 2 charters of about 50 actions each, or 3 if the first two ended early because they were blocked. Pick them as *Choose the charters* says.
- **Keep the log in your own notes as you go**, in the shape of `sessions/_TEMPLATE.md`, line by line, so nothing is lost if the task is cut short. If you sense the task is about to end (a limit, a timeout), write the hand-back below at once with what you have.
- **Ask Ernest to confirm** the form submissions your tool asks him about: that's expected. Never take over a step the safety rules forbid because a confirmation was given.
- **The stop checklist still applies**, item by item. The only change: instead of updating files, you write them in the hand-back.

**The hand-back.** End the session with one Markdown block in the chat, nothing left out, in this order:

1. `### Session log`: the whole log, in the template's shape, with the session ID (`YYYY-MM-DD-G1`, `G` for a browser-only session) and the account's label.
2. `### STATE.md changes`: each coverage row you changed (the full new row), each lead to add or remove, the new handoff note, and any baseline or fixture change.
3. `### FINDINGS.md additions`: new findings in the file's block format, with IDs left as `F-NEW-1`, `F-NEW-2` (the agent that records them numbers them), and the IDs of existing findings you saw again or found fixed.

Then say in one line how many findings you raised by severity, and repeat any S1 in full. The agent that records it writes the files, gives the findings their numbers, and opens the PR.

## Techniques

Pick the ones that fit the charter. Name the one you used in the log.

**Inputs:** boundaries (0, 1, the min and max, one past), empty and whitespace only, very long, decimals with a comma and a point, thousands separators, negatives, special characters, accents, emoji, leading zeros, pasted text with line breaks.

**Navigation and state:** Back and Forward through the browser, the app's ← link, a reload in the middle of a flow, opening an address directly (a strategy, signal or stock page), a second tab changing the same data, a stale tab acting after the data changed, double clicking a button, leaving a form half done and coming back.

**Layout:** 390px wide (the wireframes), 360px, a tablet and a wide desktop; zoom to 200%; long names in cards and top bars (they should end in "…", never wrap or scroll sideways).

**Language:** Catalan for every screen you visit at least once a session. Look for English left over, cut words, overflow, and number and date formats.

**Time:** what changed since the last session because the daily check ran (new signals, statuses, results, AI rounds due, missed signals). The fixtures exist to test this: compare today's state with the last session's notes.

**Consistency:** the same figure on the dashboard card, the strategy page, the stock page and the signal; the same rule worded the same everywhere; the same component behaving the same on every screen.

**Access:** a strategy, stock or signal ID changed by one in the address bar (it must not show another user's data); `/admin` as a vip (it must act as if it doesn't exist); signed-out access to a signed-in page (Log-in, then back to it).

**Arithmetic:** add up the trades by hand and compare with *Budget used*, the steps, the gains and the per-stock results. Check the fee (about 0.2% plus 1 in the currency, inside the step). An S1 lives here.

**Accessibility:** the keyboard only (Tab order, focus visible, Enter and Space), visible labels on fields, touch targets at least 44 × 44px, contrast that looks too low.

**Errors:** what the app says when a field is refused, a page has no data, or a request fails (go offline in the browser for a moment, if the browser lets you).

## Oracles: how you know something is wrong

Check these on every screen you reach. A break is a finding.

**STL's product rules** (CLAUDE.md, *Product rules*):

1. The site footer ends with "Not financial advice. Signals follow your rules." and the legal links. Screens don't add other disclaimer lines.
2. No instruction or urgency: no "buy now", no price targets or return forecasts, no "recommended for you".
3. Results always sit next to the index fund and the worst year.
4. Every strategy shows its mode, *Simulation* or *Live*. Results of the two are never mixed.
5. The words are *stock*, *fund* and *step*, never *tranche*.
6. No horizontal scrolling at any width. Touch targets at least 44 × 44px. Readable contrast.

**The spec:** the copy, states and behaviour the spec describes for that screen (copy may be shorter or better, but not different in meaning). A difference is a finding, unless the spec looks wrong: then say so in the finding.

**Consistency:** with itself (the same data in two places), with similar screens, with the app's other modes and templates, and with what it said a step earlier.

**Common sense, as a user:** would a first-time user understand what to do and what happened? Is anything surprising, slow or ugly? Write it down as an S4 **idea**, even if no rule is broken.

## Writing the log

Write as you go, not at the end. One line per action group or check, short. Example:

```
C2.3 Budget: typed 0 → "Enter an amount" under the field, Continue off. OK (spec §4).
C2.4 Budget: typed 1000,50 → shows €1,000.50 in Confirm; Catalan shows 1.000,50 €. OK.
C2.5 Budget: steps 7 × €200 = €1,400 > budget €1,000 → no warning. LEAD L-014 (spec says?).
```

Use the IDs: charters C1–C5, areas from the coverage map (B7), leads L-…, findings F-….
