import type { Backtest, Security, StrategyTemplate, TemplateDefaults, TemplateDetail } from '../types'

// Copy from docs/strategy-trading-lab-app-spec.md, sections 5 and 6.
// Starter lists, funds and backtest figures are MOCK values until the POC's lists and the backtest engine are wired in.

const winners: Security[] = [
  { id: 'mock-NVDA', symbol: 'NVDA', name: 'Nvidia', what: 'Chips for graphics and AI' },
  { id: 'mock-AVGO', symbol: 'AVGO', name: 'Broadcom', what: 'Chips and infrastructure software' },
  { id: 'mock-META', symbol: 'META', name: 'Meta Platforms', what: 'Facebook, Instagram and WhatsApp' },
  { id: 'mock-NFLX', symbol: 'NFLX', name: 'Netflix', what: 'Streaming films and series' },
  { id: 'mock-ZS', symbol: 'ZS', name: 'Zscaler', what: 'Cloud security software' },
]

const beatenDown: Security[] = [
  { id: 'mock-INTC', symbol: 'INTC', name: 'Intel', what: 'Computer chips and chip factories' },
  { id: 'mock-NKE', symbol: 'NKE', name: 'Nike', what: 'Sports shoes and clothing' },
  { id: 'mock-PYPL', symbol: 'PYPL', name: 'PayPal', what: 'Online payments' },
  { id: 'mock-BA', symbol: 'BA', name: 'Boeing', what: 'Passenger planes and defence' },
  { id: 'mock-EL', symbol: 'EL', name: 'Estée Lauder', what: 'Skin care and make-up' },
]

const aiWatchList: Security[] = [
  { id: 'mock-AAPL', symbol: 'AAPL', name: 'Apple', what: 'Phones, computers and services' },
  { id: 'mock-MSFT', symbol: 'MSFT', name: 'Microsoft', what: 'Software and cloud computing' },
  { id: 'mock-GOOGL', symbol: 'GOOGL', name: 'Alphabet', what: 'Google search, YouTube and cloud' },
  { id: 'mock-AMZN', symbol: 'AMZN', name: 'Amazon', what: 'Online shop and cloud computing' },
  { id: 'mock-NVDA', symbol: 'NVDA', name: 'Nvidia', what: 'Chips for graphics and AI' },
  { id: 'mock-META', symbol: 'META', name: 'Meta Platforms', what: 'Facebook, Instagram and WhatsApp' },
  { id: 'mock-TSLA', symbol: 'TSLA', name: 'Tesla', what: 'Electric cars and batteries' },
  { id: 'mock-ZS', symbol: 'ZS', name: 'Zscaler', what: 'Cloud security software' },
]

const byName = (list: Security[]) => [...list].sort((a, b) => a.name.localeCompare(b.name))

const howTested = {
  provider: 'Yahoo Finance',
  years: '2010-2024',
  feePerTrade: 1,
  feePercent: 0.2,
}

const stockDefaults = (params: TemplateDefaults['params']): TemplateDefaults => ({
  version: 1,
  budget: { amount: 3000, step: 100, maxSteps: 5 },
  params,
})

const stockTestedDefaults = 'The tested defaults: €3,000 budget, €100 steps, up to 5 per stock.'

const backtest = (b: Omit<Backtest, keyof typeof howTested>): Backtest => ({ ...howTested, ...b })

export const templateDetails: TemplateDetail[] = [
  {
    key: 'dip',
    engineKey: 'stage2_pullback',
    name: 'Dip in winners',
    tagline: 'Find the next Nvidia',
    idea: 'A stock on a strong run takes a small step back. The strategy signals that dip.',
    investsIn: 'stockList',
    signals: 'buysOnly',
    aiRound: null,
    rationale:
      'Pick winning stocks: companies whose price has climbed steadily for months because the market believes in them. Then wait for a short pullback, when the price dips a few percent.',
    rules: [
      { marker: '01', text: '**The long trend is up:** the stock is above its `SMA 200` (average price over the last 200 days).' },
      { marker: '02', text: '**Wait for a discount:** it drops into the discount zone, `5-15%` below its recent peak.' },
      { marker: '03', text: '**The dip is not a collapse:** it is still above its `SMA 50` (average price over the last 50 days).' },
    ],
    watches: { kind: 'stocks', starterList: byName(winners) },
    defaults: stockDefaults({
      minGainPct: 50,
      gainDays: 168,
      peakDays: 60,
      zoneStartPct: 5,
      zoneEndPct: 15,
      trendDays: 200,
      floorDays: 50,
      cooldownDays: 10,
    }),
    backtest: backtest({
      scope: '5 stocks, 2010-2024.',
      universe: '5, the starter list',
      testedDefaults: stockTestedDefaults,
      periods: [
        { period: '2010-14', template: 38, index: 61, worstYear: -9 },
        { period: '2015-19', template: 71, index: 58, worstYear: -12 },
        { period: '2020-24', template: 64, index: 79, worstYear: -21 },
        { period: 'All', template: 212, index: 248, worstYear: -21 },
      ],
    }),
  },
  {
    key: 'comeback',
    engineKey: 'turnaround',
    name: 'The comeback',
    tagline: 'Stocks on the rise',
    idea: 'A stock that fell a lot starts to recover. The strategy looks for a confirmed turnaround.',
    investsIn: 'stockList',
    signals: 'buysOnly',
    aiRound: null,
    rationale:
      'A stock that fell a long way starts to climb again. Instead of guessing the bottom, the strategy waits until the turnaround looks confirmed, then signals buys in steps.',
    rules: [
      { marker: '01', text: '**It was beaten down:** the stock fell at least `30%` from its high of the last year.' },
      { marker: '02', text: '**It turned around:** it climbs `15%` above its `SMA 50` (average price over the last 50 days).' },
      { marker: '03', text: '**More people are trading it:** its volume (shares traded per day) is rising.' },
    ],
    watches: { kind: 'stocks', starterList: byName(beatenDown) },
    defaults: stockDefaults({
      drawdownPct: 30,
      belowSma200: true,
      candidateDays: 60,
      aboveSma50Pct: 15,
      volumeRatio: 1.2,
      maxChasePct: 8,
      nearHighPct: 15,
      breakoutPullbackPct: 8,
      cooldownDays: 5,
    }),
    backtest: backtest({
      scope: '5 stocks, 2010-2024.',
      universe: '5, the starter list',
      testedDefaults: stockTestedDefaults,
      periods: [
        { period: '2010-14', template: 22, index: 61, worstYear: -14 },
        { period: '2015-19', template: 31, index: 58, worstYear: -17 },
        { period: '2020-24', template: 47, index: 79, worstYear: -26 },
        { period: 'All', template: 118, index: 248, worstYear: -26 },
      ],
    }),
  },
  {
    key: 'bear',
    engineKey: 'bear_accumulation',
    name: 'The big market drop',
    tagline: 'When the market is on sale',
    idea: 'When the whole market falls 20%, the strategy signals buys of a broad S&P 500 fund in small steps as it recovers.',
    investsIn: 'sp500Fund',
    signals: 'buysOnly',
    aiRound: null,
    rationale:
      'Big market crashes are rare and scary, and most people sell. Instead of guessing the bottom, the strategy waits for the first bounce and then signals buys in small steps while prices are still low.',
    rules: [
      { marker: '01', text: '**The market fell hard:** the S&P 500 is `20%` or more below its high.' },
      { marker: '02', text: "**The first bounce:** it climbs `5%` off its low. That's the first step." },
      { marker: '03', text: '**Then in steps:** one more step every `2 weeks`, until the budget is used.' },
    ],
    watches: {
      kind: 'fund',
      fund: { id: 'mock-SPY', symbol: 'SPY', name: 'SPDR S&P 500 ETF', what: 'Tracks the 500 largest US companies' },
    },
    defaults: {
      version: 1,
      budget: { amount: 3000, step: 600, maxSteps: 5 },
      params: { drawdownPct: 20, highDays: 126, reboundPct: 5, stepEveryDays: 10, rearm: true },
    },
    backtest: backtest({
      scope: '3 big drops, 2000-2024.',
      universe: 'The S&P 500 fund',
      testedDefaults: 'The tested defaults: €3,000 budget, 5 steps of €600.',
      periods: [
        { period: '2000-09', template: 41, index: -9, worstYear: -18 },
        { period: '2010-19', template: 12, index: 189, worstYear: -4 },
        { period: '2020-24', template: 58, index: 79, worstYear: -12 },
        { period: 'All', template: 136, index: 283, worstYear: -18 },
      ],
    }),
  },
  {
    key: 'trend',
    engineKey: 'index_trend',
    name: 'Ride the long trend',
    tagline: 'Take the wins, dodge the losses',
    idea: 'The strategy signals a buy of a broad index fund when the market moves into an uptrend, and a sale when the trend turns down.',
    investsIn: 'indexFund',
    signals: 'buysAndSells',
    aiRound: null,
    rationale:
      'Markets move in long waves. The strategy signals a buy of a broad index fund when the market moves into an uptrend, and a sale when the trend turns down, to dodge the dip. After the dip it looks for a new buy signal.',
    rules: [
      { marker: 'BUY', tone: 'buy', text: 'The market closes more than `3%` above its `SMA 200` (average price over the last 200 days).' },
      { marker: 'SELL', tone: 'sell', text: 'The market closes more than `3%` below its SMA 200.' },
      { marker: 'ALL', text: 'All in, all out: each signal covers the whole budget at once.' },
    ],
    watches: {
      kind: 'fund',
      fund: { id: 'mock-VTI', symbol: 'VTI', name: 'Vanguard Total Stock Market ETF', what: 'The whole US stock market, large and small' },
    },
    defaults: {
      version: 1,
      budget: { amount: 3000, step: null, maxSteps: null },
      params: { smaDays: 200, bandPct: 3, cooldownDays: 5 },
    },
    backtest: backtest({
      scope: '2000-2024, 23 buys and sells.',
      universe: 'The index fund',
      testedDefaults: 'The whole €3,000 budget, all in, all out.',
      periods: [
        { period: '2000-09', template: 24, index: -4, worstYear: -8 },
        { period: '2010-19', template: 121, index: 186, worstYear: -11 },
        { period: '2020-24', template: 52, index: 81, worstYear: -9 },
        { period: 'All', template: 249, index: 272, worstYear: -11 },
      ],
    }),
  },
  {
    key: 'ai',
    engineKey: null,
    name: "Your AI's top picks",
    tagline: 'Let your AI do the reading',
    idea: 'Every 2 weeks the AI scores each stock from 0 to 100, and the top score should get a position. Use your own AI.',
    investsIn: 'stockList',
    signals: 'buysOnly',
    aiRound: { everyWeeks: 2, topN: 1 },
    rationale:
      'Nobody can read every news story and financial report about every company, but an AI can. You run our prompt in the AI you already use; it scores each stock, and the strategy signals the top one. You see whether all that reading turns into better picks.',
    rules: [
      { marker: '01', text: 'Every `2 weeks` you run the AI round: copy the prompt, paste it into your AI, paste its answer back.' },
      { marker: '02', text: 'The stock with the top score gets a signal for one step, until the budget is used.' },
      { marker: '03', text: "If you skip a round, there's no signal that round." },
    ],
    watches: { kind: 'stocks', starterList: byName(aiWatchList) },
    defaults: stockDefaults({ everyWeeks: 2, topN: 1 }),
    backtest: null,
  },
]

/** The pick-screen list: the summary fields of each template, in catalogue order. */
export const templates: StrategyTemplate[] = templateDetails.map(
  ({ key, engineKey, name, tagline, idea, investsIn, signals, aiRound }) => ({
    key,
    engineKey,
    name,
    tagline,
    idea,
    investsIn,
    signals,
    aiRound,
  }),
)
