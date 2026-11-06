# QA findings

Every problem the QA sessions found, newest last. IDs never change or get reused. Ernest moves the ones worth fixing to GitHub issues and writes the issue number here; the agent marks a finding **fixed** when a later session can't reproduce it.

Severity (GUIDE, *Record findings*): **S1** money, results, modes mixed, data, security, misleading · **S2** broken, blocks the flow · **S3** wrong but works · **S4** nit or **idea**.

Status: **open** · **issue #n** · **fixed** (session) · **won't fix** (Ernest) · **not a bug** (with why).

<!-- Copy this block for each finding.

## F-000 · [short title]

- **Severity:** S3 · **Area:** B8 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** [footer version] · **Width / language:** 390px, English
- **Steps:**
  1. …
- **Expected:** … (spec §…)
- **Actual:** …
- **How often:** every time / 2 of 3
- **Notes:** where else it does or doesn't happen, the root guess.

-->

## F-001 · A sale larger than the holding is accepted and shows a false gain

- **Severity:** S3 (lowered from S1 by Ernest, 2026-11-06: it comes from a user's inconsistent entry) · **Area:** D6, D14 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. On a live strategy, log buys of 4 shares of a stock by hand, then a sale of 2.
  2. Log a sale of 10 more at $150.
- **Expected:** a warning or a refusal, or results that count only shares held. The spec lets a user record selling what they hold (§6 Strategy page, Log a trade by hand) and says nothing about selling more.
- **Actual:** saved with no warning. You +258.8% (the proceeds of all 12 shares count), Budget used $0, Invested $0. Dashboard, strategy page and stock page all agree.
- **How often:** 1 of 1
- **Notes:** deleting the sale restores the result. It needs a typo, or shares bought before the strategy. Still open for Ernest: refuse, warn, or cap. Lead L-003.

## F-002 · Budget ignores a decimal comma or point: 1000,50 becomes $100,050

- **Severity:** S3 · **Area:** B8 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. On Set your budget, type `1000,50` or `1000.50` in Budget and press Tab.
- **Expected:** €1,000.50 rounded or refused, not 100 times the amount (§6.4 limits only).
- **Actual:** $100,050. It carries to Confirm.
- **How often:** 2 of 2
- **Notes:** the trade price field reads the same input correctly, so this field needs that parser. `-2500` also becomes $2,500.

## F-003 · A malformed price, 228,5,0, saves as €22,850

- **Severity:** S3 · **Area:** F1, D7 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. On Confirm a signal, type `228,5,0` as the price and save.
- **Expected:** refused under the field, as `1,234` is asked about.
- **Actual:** saved at €22,850.00; You −99.0%.
- **How often:** 1 of 1
- **Notes:** `228,50`, `1.234,50` and `1,234.50` are read correctly. Edit fixes the trade.

## F-004 · The broker-rate field refuses its own prefilled rate

- **Severity:** S3 · **Area:** F5 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. On a signal whose stock's currency differs from the strategy's, press Change on the rate line.
  2. Change nothing and press Save the trade.
- **Expected:** the rate rounded (§3: at most 3 decimals) and accepted.
- **Actual:** the field shows 0.8911999962498304 and saving says "Use up to 8 decimals."
- **How often:** 1 of 1
- **Notes:** "Reference" gets round it; the error stays up until the next save.

## F-005 · Amounts round to whole units in rows and Budget used

- **Severity:** S3 · **Area:** D5, D14, E1, C1 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. Look at any trade with cents: a $100 step ($98.80 + $1.20), or a fee of 1.40.
- **Expected:** to the cent (§3, Numbers).
- **Actual:** "· $99 (fee $1)", "· $200 (fee $1)", Budget used "$201" for $201.40, "You $99 in". The answered signal shows it right ("$98.80, fee $0.00").
- **How often:** every time
- **Notes:** the sums underneath are right; only the display rounds.

## F-006 · AI stocks read NOT A CANDIDATE or CANDIDATE, not WATCHING

- **Severity:** S3 · **Area:** G7 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. Open an AI strategy before and after its first round.
- **Expected:** "WATCHING" with "Not scored yet" or "Last score: [n]" (§6 Strategy page, AI strategies).
- **Actual:** NOT A CANDIDATE before, CANDIDATE after. For other templates NOT A CANDIDATE means no signal comes, which isn't true here: AI has no candidate rule.
- **How often:** every time, on 3 AI strategies
- **Notes:** the spec removed WATCHING for other templates and kept it here. The spec may need a decision too.

## F-007 · AI simulation row says "bought at the next open" after buying

- **Severity:** S3 · **Area:** G7, D3 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. Use a round's scores on an AI simulation, then open the strategy page.
- **Expected:** a row that says it is bought (§5: the simulation records the trade at once).
- **Actual:** SIGNAL, "Step 1 of 5, bought at the next open", with the trade already listed and "$100 in".
- **How often:** 1 of 1
- **Notes:** check after the next daily check whether it moves to BUILDING.

## F-008 · AI frequency not reflected on Review's row and Budget's line

- **Severity:** S3 · **Area:** B2e, B8 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. On the AI review, set Advanced to a round every 1 day and save.
  2. Read the Advanced row, then go on to Set your budget.
- **Expected:** "every 1 day"; "One step every 1 day uses the budget in about 30 days" (§6.2, §6.4).
- **Actual:** "Advanced settings · every 14 days · top 1" and "One step every 14 days… about 420 days". Rule 01, the prompt and Confirm are right.
- **How often:** 3 of 3, including Go live
- **Notes:** –

## F-009 · "What can go wrong" and "Not tested" missing on reviews

- **Severity:** S3 · **Area:** B2a–e · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. Read any template review.
- **Expected:** a "What can go wrong:" line on all five, and "Not tested:" on the AI one (§6.2).
- **Actual:** none. The review's code no longer uses them; git history points to #2 ("Simplify texts").
- **How often:** every time, all 5
- **Notes:** these are the screens' risk statements, so worth deciding on purpose: bring them back or take them out of the spec. Not a disclaimer rule issue (#105 covers disclaimers only).

## F-010 · How we tested contradicts itself on delisted companies

- **Severity:** S3 · **Area:** B5 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. Open How we tested on Winners at a discount.
- **Expected:** one account of what the data holds.
- **Actual:** "1,146 US stocks (S&P 500, S&P 400, Nasdaq-100, and dropouts)" above "Failed or delisted companies from decades ago aren't included, which flatters past results."
- **How often:** every time
- **Notes:** other templates not read on this page. Which one is true decides how far the past results can be trusted. Also the 1,146 stocks tested differ from the 9 on the starter list; spec §6.2c says "[N], the starter list" (changed in #119).

## F-011 · Catalan guests see templates in English

- **Severity:** S3 · **Area:** A2, X2 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 375px, Catalan
- **Steps:**
  1. Signed out, open `/ca/welcome`, then `/new`, then a review.
- **Expected:** Catalan template texts (§3).
- **Actual:** names, taglines, ideas, rules and stock lines in English inside a Catalan screen. Signed in with Catalan they are translated.
- **How often:** every time
- **Notes:** root guess: `GET /templates` answers in the account's language, and a guest has none.

## F-012 · Catalan top bar hides log-in at phone width

- **Severity:** S3 · **Area:** A7, X3 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 375px and 360px, Catalan
- **Steps:**
  1. Signed out in Catalan at 375px, open `/new`.
- **Expected:** both links visible; the title gives way first (§4).
- **Actual:** "ENGLISH…" and no visible "Inicia sessió", though the link is still in the page. English fits.
- **How often:** every time
- **Notes:** the footer still has it.

## F-013 · Catalan shows English number and money formats

- **Severity:** S3 · **Area:** X2 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, Catalan
- **Steps:**
  1. Set Catalan and read the dashboard.
- **Expected:** Catalan formats, such as 3.000 $ and 0,0 % (GUIDE example; the spec doesn't say).
- **Actual:** "$99 de $3,000", "0.0%", "−0.3%", "$203.88".
- **How often:** every time
- **Notes:** dates are Catalan ("5 de nov."). A Catalan reader may read $3,000 as three dollars.

## F-014 · Catalan mode tag mixes LIVE and REAL

- **Severity:** S3 · **Area:** X2 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, Catalan
- **Steps:**
  1. In Catalan, compare the tag on the signal page with the dashboard, strategy and stock pages.
- **Expected:** one word (#151 chose "Real" for Live).
- **Actual:** REAL on the signal; LIVE on the other three.
- **How often:** every time
- **Notes:** –

## F-015 · Catalan: legal bodies and stock descriptions in English

- **Severity:** S3 · **Area:** A6, X2 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 375px and 1470px, Catalan
- **Steps:**
  1. In Catalan, open `/legal/disclaimer` and a stock page.
- **Expected:** Catalan (§3), or a line saying the legal text is in English only.
- **Actual:** "Full statutory disclaimer:", "1. NOT FINANCIAL ADVICE" and the body in English; "Cloud security software" on the stock page.
- **How often:** every time
- **Notes:** the legal text may need a lawyer's Catalan; that is Ernest's call.

## F-016 · Catalan "Millor accions" reads as "better stocks"

- **Severity:** S3 · **Area:** X2, F1 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, Catalan
- **Steps:**
  1. On Confirm a signal in Catalan, read the button by the amount.
- **Expected:** something like "En accions" (the English is "Shares instead").
- **Actual:** "MILLOR ACCIONS", which reads like "better stocks" on the one screen where the app must not sound like advice.
- **How often:** every time
- **Notes:** –

## F-017 · AI card says results start after the first round, after one ran

- **Severity:** S3 · **Area:** C1, G7 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. Run an AI round on a live strategy and skip its signal.
  2. Read the dashboard card, then archive it.
- **Expected:** the same as the page ("No trades yet."), §6 Dashboard.
- **Actual:** "No results yet. They start after your first AI round.", also when archived.
- **How often:** 2 of 2
- **Notes:** –

## F-018 · The "i" target is 14×14px

- **Severity:** S3 · **Area:** X7 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 375px and 1470px, English
- **Steps:**
  1. Measure the "i" after "S&P 500 fund".
- **Expected:** at least 44×44px (§3).
- **Actual:** 14×14px. When open, its text also covers "How we tested".
- **How often:** every time
- **Notes:** same component on reviews and strategy pages.

## F-019 · Pick screen has no "You run [n] from this template"

- **Severity:** S3 · **Area:** B1 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. With a strategy running from a template, open "+ New strategy".
- **Expected:** "You run [n] from this template" (§4).
- **Actual:** not shown.
- **How often:** every time
- **Notes:** §9 lists it as not designed yet, so it may simply be not built.

## F-020 · Picks vs the rest reads +2.7% while the stock's result is −1.2%

- **Severity:** S3 · **Area:** G5 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1470px, English
- **Steps:**
  1. Run an AI simulation round, then open AI rounds.
- **Expected:** a figure a user can square with the strategy page.
- **Actual:** "Since then: picks +2.7% · the rest –", next to −1.2% for the same stock. +2.7% runs from the signal session's close ($327.02), before the buy at $335.90.
- **How often:** 1 of 1
- **Notes:** it follows the spec's definition; the spec may want a look. Lead L-004 for "the rest –".

## F-021 · Welcome shows made-up results with no "example" label

- **Severity:** S3 · **Area:** A1 · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** 1024px, English
- **Steps:**
  1. On Welcome, open Real-time simulation.
- **Expected:** no results that look real without saying they aren't (§3, No promises).
- **Actual:** "STRATEGY RETURN +14.2%", "+8.1%", "−12.4%" with nothing saying it is an example.
- **How often:** every time
- **Notes:** the spec asks for these figures, so the spec may be what needs changing. The live demo also says "below 52-week peak · Above SMA 200", which isn't the template's rule (recent peak, SMA 50).

## F-022 · Small copy and polish items, and ideas

- **Severity:** S4 · **Area:** many · **Status:** open
- **Found:** 2026-11-06-1 · **Seen again:** –
- **Version:** v0.52.0-alpha · **Width / language:** various
- **Steps:** as listed.
- **Expected / Actual:**
  - AI: "every 1 days", "Every 1 days", "each of the top 1"; "Tested: 14 days" on a template with no test.
  - "1 shares", Catalan "1 accions", "1 fets".
  - Budget summary: "100000 steps" with no separator; "at most 20 steps ($20,000,000)… enough for 0 stocks".
  - Budget: `-2500` silently becomes 2,500. "Amounts in Euro." has a capital and no "s" where dollars has "US dollars."
  - Confirm: preview reads "Template X · Simulation" with no "from today" (§6.5); "Stocks" labels a fund.
  - Rename: the taken-name error stays up after emptying the field; Save stays on.
  - Forgot password: a value with no `@` gets the normal answer.
  - Archived strategy still says "Removing a stock stops its signals."
  - Stock page position has no steps (§6: "steps, invested, result"). Exchange shows as "XNAS".
  - Catalan: "Saved." in English just after switching; "9 de nov.." with two dots; "Mail" for Email.
  - Guest pages log 4 `/account/me` 401s in the console on each load.
- **How often:** every time
- **Notes:** **idea:** Start with an empty name quietly uses the template's name; say so, or keep the button off. **Question:** AI cards show no worst year, having no backtest to quote; is that the intended exception to the product rule?
