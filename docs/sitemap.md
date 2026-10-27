# Strategy Trading Lab: Site Map (v1)

Updated Oct 13, 2026, to match `stab-product-user-journey.md`. The visual version is the [Strategy Trading Lab Sitemap](Sitemap.html) artifact.

> A strategy lab and monitoring side app. The user picks a **strategy template**, tunes it, and runs it as **their strategy** in one of two **modes**: Simulation (paper) or Live (they trade at their own broker and confirm each signal).
> No broker sync, no order placement, no trading advice. US-listed stocks and funds; budgets and results in euro. Free for now.

Legend: 🌐 public · 🔒 requires account · 🧩 shared component · ✏️ still to design · ⏳ later · ➕ implied (not in the journey, but needed) · ❓ open decision
Mode/template tags: **[Sim]** simulation strategies only · **[Live]** live strategies only · **[AI]** strategies from *Your AI's top picks* only

Paths are suggestions.

---

## 1. Public area (🌐)

Marketing pages are outside the user journey's scope; kept here from the earlier site map, with the new names and wording.

```
/                          Home (landing)
├── /how-it-works          How it works (templates, two modes, what the app never does)
├── /templates             Strategy template catalogue ❓ public or after sign-up
│   └── /templates/[slug]      Template preview
│       dip-in-winners · the-comeback · the-big-market-drop
│       ride-the-long-trend · your-ais-top-picks
├── /pricing               Pricing ("Free for now")
├── /about                 About
├── /faq                   FAQ
├── /contact               Contact / feedback
├── /sign-up               Create account (journey step 1)
│   └── /verify            Verify email → dashboard if signed in, otherwise Log in
├── /log-in                Log in ➕
├── /forgot-password       Reset password ➕
└── /legal
    ├── /legal/disclaimer      "Not financial advice. Signals follow your rules; you decide and trade at your own broker."
    ├── /legal/terms           Terms of use
    ├── /legal/privacy         Privacy policy (GDPR), incl. AI data sharing
    └── /legal/cookies         Cookie policy
```

| Page | Purpose | Key content |
|---|---|---|
| **Home** | Explain the lab in seconds | Pick a template, test and tune it, run it as a simulation or live; template teasers with taglines; sample daily overview; disclaimer |
| **How it works** | Set expectations | Templates vs strategies, Simulation vs Live, "the app never trades for you or connects to your broker", daily check after the US close |
| **Template preview** | Understand one template | Tagline, the idea, the rules ("It buys when…"), backtest with index comparison and worst year, what can go wrong |
| **Sign up** | Account in under a minute | Email and password, time zone (guessed), daily overview on, disclaimer accepted once |

### Templates

| Template | Tagline | Invests in | Trades |
|---|---|---|---|
| Winners at a discount | Buy the dip ❓ legal review | Individual stocks | Buys in the discount zone |
| The comeback | Stocks on the rise | Individual stocks | Buys |
| Bear markets | When the market is on sale | Broad S&P 500 fund | Buys |
| Index trend | Ride the wave | Broad index fund | Buys and sells (all in, all out) |
| Your AI's top picks | Let your AI do the reading | Individual stocks | Buys after each AI round; no backtest |

---

## 2. New strategy flow (🔒, journey steps 2–6)

Entered from sign-up (first run), from **+ New strategy** on the dashboard, and from **Go live with these settings** on a simulation (joins at step 4, prefilled).

```
/app/new
├── 2. /app/new                    Pick a strategy template (5 cards, single choice)
├── 3. /app/new/[template]         Template review (one page per template)
│      └── /advanced               Advanced settings (re-runs the backtest on every change)
├── 4. /app/new/mode               Choose the mode: Simulation (default) or Live
├── 5. /app/new/budget             Set your budget: budget, step size, max steps per stock
└── 6. /app/new/confirm            Name, preview and confirm
        → Dashboard, or [AI] the first AI round
```

| Step | Notes |
|---|---|
| Pick a template | "You run 2 from this template" on templates in use; nothing hidden |
| Template review | Idea → rules → Choose stocks to watch → Tested → What can go wrong → Advanced settings. [AI] version: what the AI reads, the prompt (editable), what you need, not tested |
| Choose the mode | Live asks for web push permission (email fallback; iPhone needs home-screen install) |
| Set your budget | Defaults €3,000 / €100 steps / 5 per stock; live summary sentence and step squares |
| Confirm | Editable name (template name by default, numbered), summary with edit links, disclaimer above the button |

---

## 3. App area (🔒, after log-in)

```
/app                                   Dashboard (home)
│   Waiting for you list · one card per strategy · + New strategy
│
├── /app/strategies/[id]               Strategy page
│   ├── /stocks/[symbol]               Stock page ✏️
│   ├── /settings                      Tune the settings (advanced settings for this strategy)
│   ├── /backtest                      Backtest ✏️
│   ├── /rounds                        [AI] AI rounds history ✏️ (replaces Backtest)
│   ├── /history                       History ✏️ (signals, trades, skips, rounds, status changes)
│   ├── Go live with these settings    [Sim] ✏️ → new live strategy at step 4
│   └── Rename · pause · end           ✏️
│
├── /app/signals/[id]                  [Live] Confirm signal sheet ✏️
│   ├── I did it                       Date, price (next day's open), amount, fee
│   └── I skipped it                   Optional reason
├── Log a trade by hand                [Live] ✏️ (also edit or delete a trade)
│
├── /app/strategies/[id]/round         [AI] AI round ✏️ (every 2 weeks, journey step 9)
│   ├── Copy prompt
│   ├── Paste answer, pick AI model
│   └── Check and use the scores       (first round adds the AI data sharing consent box)
│
├── /app/ai-comparison                 AI comparison ⏳ (results by AI model, never by stock)
│
└── /app/account                       Account ➕ (email, password, time zone, daily overview on/off)
    └── /privacy                       Data and privacy ✏️
        ├── AI data sharing on/off
        ├── What we keep
        ├── Export data
        └── Delete account
```

| Page | Purpose | Key content |
|---|---|---|
| **Dashboard** | See every strategy at a glance | Waiting for you (signals, AI rounds due); cards with name, template, mode label, result since start, same money in an index fund, small chart, budget used, badges ("1 signal waiting", "AI round due"); new strategies show the backtest result |
| **Strategy page** | Is it working? | Name, template and mode; result vs index ([Live] also vs the rules on their own); budget used; one row per stock with status (Watching, Signal, Building, Holding; [Live] Waiting for you), steps and result; [Live] rules' position vs the user's; [AI] latest scores |
| **Stock page** | One stock in one strategy | Chart with the discount zone, status history, trades ([Live] confirmed, skipped, manual, each marked) |
| **Confirm signal** | Core loop of live mode | The rule that was met and why; I did it / I skipped it; unanswered signals get one reminder, then *Missed* |
| **AI round** | Bring your own AI | Prompt filled with date, stocks and sources; paste answer; parsed scores table with the AI's reasons; consent box on first round |
| **Data and privacy** | Consent and GDPR rights | AI sharing toggle, What we keep, export, delete account |

---

## 4. Outside the app (emails and notifications)

All sent after the US market close.

| Touchpoint | Who gets it | Opens |
|---|---|---|
| **Daily overview** (email) | Everyone, on by default | Waiting for you items, strategy pages, stock pages |
| **Signal alert** (web push + email) | [Live] | Confirm signal |
| **Signal reminder** | [Live], once, if unanswered | Confirm signal |
| **AI round due** (notification) | [AI] | AI round |
| **Account emails** ➕ | Everyone | Verify email, reset password |

---

## 5. Shared components (🧩)

- Public top navigation; app navigation (Dashboard · New strategy · Account)
- Disclaimer line wherever results or signals appear, and in the footer: "Not financial advice. Signals follow your rules." The legal page and the places where the user commits use the full one (spec, section 3).
- Mode label (Simulation / Live) on every strategy
- Strategy name with template, mode and start date, e.g. "Zscaler & friends · *Winners at a discount* · Live · since Oct 13"
- Template card (name, tagline, idea, Learn more)
- Result block: result vs index fund, worst year, "Past results don't promise future ones"
- Stock status pill: Watching · Signal · Building · Holding · Waiting for you
- Waiting for you list item
- Settings row with − / + control, tested default and *Changed* mark
- Empty, loading and error states (404, 500); cookie consent banner

---

## 6. Language rules

See "Wording rules" in the user journey. In short:

- Describe, don't instruct: template names describe a situation; the strategy is the subject of trades, never the user.
- Signals report rules ("Your rules for … are met"), never "buy now", no targets or forecasts.
- The user owns the setup: "Choose stocks to watch", "Under your settings", never "recommended for you".
- No promises; results always shown with the index comparison and worst year.
- Never "ticker" or "tranche": say *stock* (or *fund*) and *step*. "Discount zone", not "buy zone".

---

## 7. Out of scope for v1

- Broker connections, order placement, reading the user's broker account
- Switching a strategy's mode in place (going live starts a new strategy)
- Selling logic, except *Index trend*
- Running AI in the app (users bring their own AI)
- Telegram alerts
- Non-US markets
- Paid plans

---

## 8. Open decisions (❓)

1. Is the template catalogue public, or visible only after sign-up?
2. Languages at launch: English only, or also Spanish / Catalan?
3. Public blog or changelog, or leave until after launch?
4. "Buy the dip" tagline: pending legal review.
5. Design for pausing and ending a strategy.
6. Web push on iPhone: is a home-screen prompt enough?
