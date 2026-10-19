# Roadmap

The backlog for Strategy Trading Lab. Agents take work from **Next**, in order, one item per PR, and move it to **Done** with its PR number when it merges. Ernest sets the order; agents may add items, ideas and open decisions, and say so in their PR.

Screens and behaviour are in the [spec](docs/strategy-trading-lab-app-spec.md). Ernest's own notes are in [TODO.md](TODO.md).

## Done

- New-strategy flow, frontend: Pick a template, Review a template (all five), How we tested, Choose the mode, Set your budget, Name, preview and confirm (#1–#3, #10, #13, #19). Advanced settings and the dashboard are placeholders.
- Backend: data model (#14), .NET skeleton with the first migration on Neon (#15), the five templates with the POC's tested defaults (#16), deployed to Oracle on merge (#17), the frontend loading templates from the API (#18).
- CI on PRs, frontend deploy to Oracle, app version in the top bar, reload on a new version (#4–#9, #11).
- Agentic workflow: instructions, ROADMAP, shared Claude settings and cloud setup hook (#20). Two disclaimers: short on screens seen again and again, full where the user commits (#21).

## Next

Draft order, for Ernest to confirm.

1. **Catalan.** A priority, and cheaper now, while there are few screens. Move the frontend's copy into per-language files (English and Catalan) with a language switch the user picks (English by default, never guessed from the browser, as the spec says), and add `Texts/ca.json` for the templates. The API already serves template texts per language, from the user's record, so it needs a way to receive the choice before accounts exist. Claude drafts the Catalan; Ernest reviews it.
2. **Save a started strategy.** `POST /strategies` from the Confirm screen, stored with its mode, budget, parameters and stocks. Needs the decision on the user before sign-up (see below).
3. **Dashboard (`Home`):** the user's strategies, each with its mode.
4. **Strategy page (`StrategyDetail`):** Live variant first, as drawn.
5. **Sign-up and log-in.** High risk. Not designed yet: design it first.
6. **Market data:** daily prices and the euro-dollar rate, once the provider is chosen.
7. **Daily evaluation and signals** (high risk), then **Confirm a signal** (`Signal`).
8. **Advanced settings** for *Dip in winners*, replacing the placeholder.

## Later

From the spec's *Not designed yet* list, roughly in the order they become useful:

- Simulation variants of Set your budget, Confirm and the strategy page.
- Notifications: web push and the daily overview email.
- Stock page; stock list editor, stock search and fund picker.
- Manual trades; editing and deleting trades.
- Strategy actions: go live with these settings, rename, change the budget, notifications switch, end.
- Sell signals and "Out of the market" for *Ride the long trend*.
- AI rounds: prompt editor, history, error state, comparison page.
- Backtest page and History.
- Account page, data and privacy (AI sharing, export, delete account).
- Empty, loading and error states everywhere.

## Ideas

- AI automation for continuous improvement.
- Users can give feedback.
- Referral program.

## Open decisions (Ernest)

- **A user before sign-up:** to save strategies before log-in exists, use one fixed development user, or build sign-up first?
- **Price data provider:** terms that allow commercial use, and the cost at many free users. The POC uses eToro and Yahoo.
- **Catalan copy:** Claude drafts, Ernest reviews. Is that enough, or does a native speaker review it before release?
- The rest are in the spec's [Open questions](docs/strategy-trading-lab-app-spec.md#10-open-questions): regulation, AI data, the suggested prompt, web push on iPhone.
