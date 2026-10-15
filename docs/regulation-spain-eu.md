# Strategy Trading Lab: Spanish and EU regulation

Oct 13, 2026 · @Ernest

This note covers how Strategy Trading Lab (simulation and live modes, as described in `claude/stab-product-user-journey.md`) fits the EU and Spanish rules on investment services. It also covers what changes if the app grows and makes money. It is research, not legal advice. Before charging money, get a written opinion from a Spanish securities lawyer. The questions to bring are at the end.

## The short answer

Three sets of rules matter.

1. **Investment advice is a licensed activity** (MiFID II in the EU, Ley 6/2023 in Spain). Giving *personal* recommendations about specific stocks needs CNMV authorisation. Doing it without one is a very serious infringement. The fine for breaching the reserved-activity rule can't be below €600,000.
2. **Investment recommendations to the public need no licence, but they have rules** (the Market Abuse Regulation, MAR). They must be presented objectively, say who made them, and disclose conflicts of interest. The CNMV actively checks this, today mostly on finfluencers.
3. **Marketing and paid referrals.** In Spain, only licensed firms or their registered agents may get paid for bringing clients to a broker.

How the app fits today:

| Part of the app | Risk | Why |
| --- | --- | --- |
| Strategy catalogue, review pages, backtests | Low | General, educational information about how rules behaved on past data. |
| Simulation mode | Low | Paper results of rules, the same for everyone. Nobody is told to do anything. |
| Live mode, rule-based strategies | Medium, manageable | Signals go to one user, in real time, about a specific stock. That stays outside "advice" only if the signals are the mechanical result of rules the user picked and set, are not presented as suitable for them, and use nothing about their personal situation. |
| AI strategy, bring-your-own-AI | Low to medium | The app gives a suggested prompt; the user runs it in their own AI service and pastes the scores back. The opinion about the stocks comes from the user's AI, not from the app, and the app applies the user's rule ("top score gets a step") to it. What remains is our content: the prompt and the default stock list. Section 1 has the details. |
| Paid broker referral links | Not allowed as is | Per-client referral fees count as client acquisition, which is reserved for licensed firms and their tied agents. |
| A paid subscription for the tool | Low | The CNMV says selling investment software to retail users needs no authorisation, as long as the company doesn't set it up from the client's information. |

"Buy the next Nvidia" → "Find the next Nvidia" helps, but the tagline doesn't decide the licensing question. The structure of live signals does. Section 4 has the wording changes.

## 1. When does a signal become investment advice?

MiFID II defines investment advice as a *personal recommendation* about transactions in specific financial instruments. The Delegated Regulation sets the test. A personal recommendation is made to someone as an investor and is either *presented as suitable for that person* or *based on a consideration of their circumstances*. A recommendation *issued exclusively to the public* is not personal.

ESMA's 2023 supervisory briefing on the definition of advice is the most useful guide, and three points in it matter most for this app.

**Information versus recommendation.** Plain facts and figures are information. A recommendation needs an element of suggestion, and it can be implicit. ESMA lists *alerts, including alerts created by software, about certain events* (for example, a share reaching a price) as information that is not necessarily a recommendation. The catch is the multi-step case. An alert that a price was reached is a personal recommendation if it follows a *prior recommendation* to buy at that price. **For the app, this means:** a live signal is safest as the report of a rule the *user* chose and set ("your rule is met"). It is riskiest as the second half of a recommendation the app made first ("we picked these stocks for you; now buy").

**Filtering by the user's own criteria.** ESMA says letting users filter products by criteria they choose is unlikely to be advice. What matters is that the process helps people make their own choice and shows no judgement about which choice they should make. The CNMV's FinTech Q&A (question 4.4) puts it in software terms. Selling an algorithm or software directly to retail investors needs no CNMV authorisation. If the seller *also sets its parameters based on information from the client*, that is investment advice. **For the app, this means:** strategy settings, stock lists and budgets must be the user's own, chosen from defaults that are identical for everyone. The app must never fit them to the user's situation.

**"Issued exclusively to the public" doesn't protect live alerts.** ESMA says recommendations through websites, apps and private messages can be personal. This applies especially when they are addressed to one person, or when targeted messages carry personalised information. An alert sent to one user, mentioning their budget and their step 2 of 5, is not "to the public". The app can't rely on that exemption for live mode. It relies on the other test: **not presented as suitable, and not based on the user's circumstances.**

**What counts as "circumstances".** ESMA means facts (income, address, family situation) and also wants and needs (risk appetite, investment goals). A firm that has collected this information can't avoid it with a disclaimer. If the user could reasonably expect the information to be used, later recommendations count as based on it. **For the app, this means:**
- No onboarding questions about income, savings, risk tolerance, goals or age.
- No "recommended for you" strategies.
- The budget is a parameter of the user's rule, not something the app suggests based on who they are. In a related Q&A on investment comparison sites, the CNMV accepted asking only for the investment amount, the time horizon and the generic product type.

**The trades users confirm are financial information about them.** Live mode records what the user really bought, which describes part of their financial situation. Recording it for their own tracking is fine. Using it to change what signals say is where a regulator could argue the signals have become personal. That affects the open question in the journey about whether, after a skipped buy, signals follow the rules' paper position or the user's real one. The paper position is the cleaner choice legally: the signal is then the same for everyone running the same rules on the same stocks, whatever they actually did.

**The bring-your-own-AI design.** The AI strategy no longer runs an AI in the app. It gives the user a suggested prompt, the user runs it in their own AI service (ChatGPT, Claude, Gemini or similar), and pastes the scores back. The app then applies the strategy's rule: the top score gets a step. This moves the AI strategy from the riskiest part of the app to roughly where the rule-based strategies sit, for three reasons:
- **The opinion isn't ours.** The scores come from a service the user chose, runs and pays for. The app never forms or publishes a view on which stock is best; it reads numbers the user brings and applies a rule they picked. It is close to the CNMV's "software sold to the client, set up by the client" case.
- **Nothing personal goes in.** The prompt holds only the date, the stock list and the sources switched on. It never includes the user's budget, holdings, goals or anything else about them, so the scores can't be "based on their circumstances" through us.
- **The same prompt for everyone.** It is a template, like a strategy's tested defaults, and the user can read and edit it.

Two things still carry some risk:
- **The prompt is our content.** A prompt that asked "which of these should I buy?" would be us framing a recommendation; one that asks for scores against stated criteria, with reasons and dated sources, is a research method. Keep it the second kind, and publish it on the strategy's Methodology page.
- **The AI service's own rules.** The user's AI provider decides whether and how it answers investment questions. That is between the user and their provider, but the app shouldn't promise that any given AI will answer, or that its scores are reliable.

If the app later runs the prompt for the user (e.g. through an API, to save them the copy-paste), the scores become the app's output again and the earlier, higher-risk analysis applies.

**Copy trading and auto-execution are out of scope, and must stay so.** ESMA and the CNMV treat automatic execution of signals as portfolio management. The exceptions are when the client authorises each trade, or when the client sets the criteria. The CNMV says "social trading" platforms that let people replicate other investors' strategies need a licence. The app's "no broker sync, the user confirms each trade by hand" rule keeps it clear of this. Sharing or copying other users' strategies would bring it back in.

## 2. The investment-recommendation rules (MAR)

These apply with or without a licence. MAR defines an investment recommendation as information that recommends or suggests an investment strategy for one or more financial instruments, explicitly or implicitly, *intended for distribution channels or for the public*. ESMA and the CNMV read this broadly, and plain language doesn't take a message out of it. The CNMV judges the substance of what is communicated, whatever it is called and however it is sent.

**Does the app produce them?** Probably yes, in places:
- The **daily overview and live signals** say a specific stock now meets the conditions of a strategy whose purpose is to buy. That is arguably an implicit suggestion.
- The **default stock lists** are a suggested set of stocks for each strategy.
- The **AI scores** are no longer the app's: each user's AI produces them and only that user sees them. That changes if the app ever aggregates or publishes them, e.g. "the stocks users' AIs rated highest this month" or a comparison of AI services across users. That would be the app publishing an opinion about stocks to many people.

Each of the first two is generated the same way for many users, which fits "distribution channels" well.

MAR covers instruments traded on EU venues. Most large US stocks and US-listed ETFs also trade on EU venues such as Tradegate or Xetra, so treat the app's universe as in scope.

**What the rules require** (Delegated Regulation 2016/958):
- **Identity.** Say who produces the recommendations: the name of the company or person.
- **Date and time** of each recommendation.
- **Facts kept apart from opinion.** Sources indicated. Any projection labelled as one, with its assumptions.
- **Conflicts of interest**, disclosed in or next to the recommendation. Since you trade yourself, this includes your own positions in stocks the app signals. ESMA warns that a link to a disclosure may not be enough.

**The fuller set.** If the app's *main business* is producing recommendations, or it presents itself as expert, it must also include:
- a summary of the methodology and assumptions;
- what "buy" means and the time horizon, with a risk warning;
- how often recommendations are updated;
- a 12-month record of past recommendations.

Strategy Trading Lab already plans most of this as product features: the review page's rules, the methodology, the History page, the backtest. Building them to this standard costs little and is good product anyway.

## 3. Monetising: what each model means

| Model | What it triggers | Verdict |
| --- | --- | --- |
| Free, or donations | MAR presentation and disclosure rules only | Fine |
| Subscription for the tool: simulation, backtests, alerts on the user's own rules, the AI round with the user's own AI | No licence, per CNMV FinTech Q&A 4.4. Spanish consumer law for paid digital services. | Fine, if the live-mode design rules below hold |
| Paid research, newsletter or app-produced scores published to all subscribers | MAR, fuller set of rules. The fair, clear and not-misleading rule applies to all marketing. | Possible, with care |
| Running the AI for the user (scores produced by the app) | Back to the app forming an opinion on specific stocks; personal if sent to one user to act on | Needs the legal review below; likely published research at best, advice at worst |
| Broker affiliate links with per-signup or revenue-share fees | In Spain, paid client acquisition for investment firms is reserved to licensed firms and their tied agents. The CNMV calls affiliate programmes inadmissible otherwise. Its Dec 2025 criterion: a fixed fee per ad is advertising, while fees per client or per volume count as acquisition. | Not allowed as is. Fixed-fee sponsorship is possible. A tied agent can work for one broker group only. |
| Personalised signals or picks (fitted to the user, or presented as suitable for them) | Investment advice: needs authorisation | Needs a licence. The lightest route is an **EAFN** (Spain-only advisory firm, no EU passport): either €50,000 capital or professional liability insurance of €1M per claim and €1.5M per year. A natural person can hold one, with insurance. The CNMV has up to 6 months to decide. The alternative is partnering with a licensed firm. |
| Executing trades or connecting brokers to act on signals | Portfolio management or reception and transmission of orders | Needs a full investment firm licence. Avoid. |

Bring-your-own-AI also keeps AI costs at zero, which suits a free app.

The EU's Retail Investment Strategy was agreed in December 2025 and should be published by January 2027, applying from around mid-2029. It tightens marketing rules and how investment firms use finfluencers. It mostly matters to the app if it ever markets on behalf of a broker or fund.

## 4. Wording: taglines, signals, claims

Rewording alone doesn't change whether something is advice, but it matters in two ways. It keeps marketing fair, clear and not misleading. And it shapes how a reasonable user reads a signal, which is exactly how ESMA's "presented as suitable" test is judged. The CNMV's advertising rules for regulated firms say superlatives and performance claims need objective, verifiable support. That is a good bar to borrow even though the app isn't a regulated firm.

| Current | Issue | Suggestion |
| --- | --- | --- |
| "Buy the next Nvidia" | It reads as a promise of exceptional returns. "Buy" is also an instruction. | "Find the next Nvidia" is better: it is a search, not an order. Safest is to drop the return image: "Strong stocks, on a short break" or "Winners, at a discount". A real company name in marketing also raises trademark questions. |
| "On the rise" / "Stocks on the rise" | Fine | Keep either one |
| "When the market is on sale" | Mild, descriptive | Keep |
| "Ride the wave" | Fine | Keep |
| "Let your AI do the reading" | Fine; "your" also makes clear whose AI it is | Keep |
| The suggested prompt | Our content, so the same care applies | Ask for scores against stated criteria, one-line reasons and dated sources. Never "which should I buy?" or "what's best for me?". |
| "Broad markets have so far always recovered from them" | A historical claim that reads as a guarantee | "In past big drops, broad markets recovered, sometimes only after years. That may not happen again." |
| Signal: "Zscaler is in the buy zone… Your plan: one step of €100" | "Buy zone" plus "your plan" reads as a call to act | "Your rules for *Buy the dip in winners* are met for Zscaler: 8% below its recent peak, still above its 50-day average. Under your settings that's step 2 of 5 (€100)." It states which rule fired and why, and keeps the user as the author of the rule. |
| Status "Buy signal" | Acceptable as the name of a rule state | "Rule met" or "Signal" is more neutral. Keep the text label either way. |
| Disclaimer | Necessary, but not sufficient | ESMA and the CNMV both say "this is not investment advice" doesn't protect you if the substance is advice. Keep it, and rely on the design rules below. |

## 5. Other rules that apply

- **Data protection (GDPR, Spain's LOPDGDD).** Accounts and confirmed trades are personal data. Financial data isn't a special category, but it is sensitive in practice. You need a privacy policy, processor agreements with hosting and email providers, EU hosting where possible, and export and delete-account features (already in the sitemap). The AI round helps here: the prompt sent to the user's AI carries no personal data, and the app never touches the user's AI account.
- **Spanish e-commerce law (LSSI-CE).** The site needs a legal notice with the owner's identity, tax ID and contact details, plus cookie consent (already in the sitemap).
- **Consumer law, once paid.** Clear pricing and terms in Spanish. The 14-day right of withdrawal applies to digital services unless the user expressly waives it when service starts.
- **EU AI Act.** With bring-your-own-AI, the app doesn't provide or deploy an AI system for scoring at all; it is the user's use of their own service. If the app later runs AI itself, scoring *companies* is still not in the high-risk list (the finance use case there is credit scoring of *people*), and the transparency duties apply: say clearly which output comes from AI.
- **Price data licences.** These are not financial regulation but real. Redistributing exchange prices commercially usually needs a licence, and Yahoo's terms don't allow commercial use. Choose the provider with this in mind.
- **Crypto (MiCA).** Out of scope while the app stays with stocks and funds. Adding crypto would bring its own advice rules.

## 6. Design rules to adopt

These keep live mode on the information-and-tools side of the line, and they cost little.

1. **The user is the author of every running strategy.** They pick it, accept or change its stocks and settings, and set the budget. Defaults are the same for everyone and presented as a starting point ("the settings we tested"), never as suited to the user.
2. **Never ask about, store or use the user's circumstances**: income, savings, goals, risk tolerance, age, experience. No "recommended for you", "people like you", "best for you" or ranking of strategies for a user.
3. **Signals report rules, not opinions.** Every alert names the rule that fired, the facts that met it, and the date and time. No "buy now", no urgency, no price targets or return forecasts.
4. **Don't let the user's real trades change the signals.** Live signals follow the strategy's rules (its paper position). Confirmed trades only feed the user's own results.
5. **Default stock lists are a universe, not picks.** Show them as "the stocks we tested this strategy on" and order them neutrally (e.g. alphabetically). Make adding and removing easy. Avoid labels like "top" or "best".
6. **AI strategy: keep the AI the user's.**
   - The prompt is a published, versioned template: same for every user, readable and editable, asking for scored criteria with reasons and dated sources.
   - It contains nothing about the user: no budget, holdings or goals.
   - The app reads only the scores block from the pasted answer and labels everything as "your AI's scores".
   - Don't run the AI for the user, and don't aggregate or publish scores across users, without a fresh legal look.
7. **Build the MAR requirements in:**
   - a public **Methodology** page per strategy (including the AI prompt);
   - an **About** page naming who runs the app;
   - a **conflicts of interest** statement, including the founder's own holdings in signalled stocks;
   - a 12-month **History** of signals;
   - dates and times on every signal.
8. **No affiliate or referral fees from brokers**, and no broker links in signals.
9. **Never automate execution**, connect to brokers to act, or let users copy other users' running strategies.
10. **Backtests and results are honest:** several periods, worst year, comparison with an index fund, "past results don't promise future ones" next to every result. The specs already require this.

## 7. Path by stage

- **Quiet launch (friends, free).** Low enforcement risk if the design rules above hold. The AI strategy can run in both modes with the bring-your-own-AI design. Add the About and conflicts statement, even if short.
- **Public beta (still free).** Add the Methodology and History pages to MAR standard, with the AI prompt published. Rework taglines and signal wording. Have a lawyer review the live-mode flow, the prompt and the copy (a few hours of work). The CNMV also takes queries from fintech promoters through its FinTech portal; a written answer there is the strongest comfort available.
- **Monetising.** A subscription for the tool is the low-risk model, and bring-your-own-AI keeps AI costs out of it. Running the AI for users, or anything fitted to the user, needs legal review and possibly an EAFN or a licensed partner. Broker referral income needs a tied-agent arrangement with one broker group.

## 8. Questions for a lawyer (or the CNMV)

1. With the design rules above, are live signals on user-configured rules information or software (FinTech Q&A 4.4), or personal recommendations?
2. Does the user's chosen budget and step size, shown in the alert, count as "circumstances"?
3. Is the app a person "whose main business is to produce investment recommendations" under MAR, and so subject to the fuller set of rules?
4. Do the default stock lists, chosen by us and shown to all users, count as investment recommendations?
5. With bring-your-own-AI, is the app's role (supplying the prompt and applying "top score gets a step" to scores the user pastes in) software, or does the suggested prompt make the resulting signal the app's recommendation?
6. Would a fixed-fee sponsorship from a broker count as advertising rather than client acquisition, given the app also sends signals?
7. Does the founder's own trading in signalled stocks need more than disclosure (e.g. a waiting period after signals go out)?

## Sources

- [Commission Delegated Regulation (EU) 2017/565, Art. 9 and recitals 14-17](https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX%3A02017R0565-20220802): definition of personal recommendation.
- [ESMA supervisory briefing on the definition of advice under MiFID II (ESMA35-43-3861, 2023)](https://www.esma.europa.eu/sites/default/files/2023-07/ESMA35-43-3861_Supervisory_briefing_on_understanding_the_definition_of_advice_under_MiFID_II.pdf)
- [ESMA supervisory briefing on copy trading (ESMA35-42-1428, 2023)](https://www.esma.europa.eu/sites/default/files/2023-03/ESMA35-42-1428_Supervisory_Briefing_on_Copy_Trading.pdf)
- [CNMV, Preguntas y respuestas dirigidas a empresas FinTech](https://www.cnmv.es/docportal/Legislacion/FAQ/QAsFinTech.pdf): Q 4.3 social trading, Q 4.4 selling software. The answers cite the pre-2023 law (TRLMV); the reasoning carries over to Ley 6/2023.
- [CNMV review of finfluencers' activity](https://www.cnmv.es/webservices/verdocumento/ver?t=%7B0ada4936-561e-4e5b-abde-c2a8802b1eb7%7D) and [CNMV note on investment recommendations by influencers](https://www.cnmv.es/webservices/verdocumento/ver?t=%7Bd6e36d88-f319-4671-98ca-fa8f1cecc112%7D)
- [ESMA/CNMV finfluencer guide (Spanish)](https://www.cnmv.es/webservices/verdocumento/ver?t=%7B36c12369-81c0-46e2-ac4b-c29851ba229c%7D): disclaimers don't protect.
- [ESMA statement on investment recommendations on social media](https://www.esma.europa.eu/sites/default/files/library/esma70-154-2780_esmas_statement_on_investment_recommendations_on_social_media.pdf) and [ESMA warning with examples (2024)](https://www.esma.europa.eu/sites/default/files/2024-02/ESMA74-1103241886-912_Warnings_on_Social_Media_and_Investment_Recommendations.pdf)
- [ESMA Q&A on MAR, section 8](https://www.esma.europa.eu/sites/default/files/library/esma70-145-111_qa_on_mar.pdf): MAR Art. 3(1)(34)-(35).
- [Commission Delegated Regulation (EU) 2016/958](https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:32016R0958): presentation and disclosure of recommendations.
- [CNMV communication on affiliate programmes](https://www.cnmv.es/webservices/verdocumento/ver?t=%7B216194b3-c81a-41ca-bb32-6dff704b54e9%7D) and [Forbes España on the CNMV's Dec 2025 criterion on influencers and client acquisition](https://forbes.es/economia/846027/cnmv-define-cuando-la-publicidad-de-influencers-sobre-servicios-de-inversion-es-captacion-de-clientes/)
- [Real Decreto 813/2023, Art. 5](https://www.boe.es/buscar/doc.php?id=BOE-A-2023-22763): EAFN regime.
- [Ley 6/2023](https://boe.es/buscar/act.php?id=BOE-A-2023-7053), and [Art. 312 on sanctions](https://www.iberley.es/legislacion/articulo-312-mercados-valores-servicios-inversion)
- [European Commission call for advice on the Retail Investment Strategy (2026)](https://www.esma.europa.eu/sites/default/files/2026-08/Call_for_advice_on_RIS_to_ESMA.pdf): timeline.
- [EU AI Act, Annex III](https://ai-act-service-desk.ec.europa.eu/en/ai-act/annex-3)
