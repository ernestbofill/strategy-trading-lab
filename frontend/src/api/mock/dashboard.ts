import { isoDate } from '../../lib/format'
import type { Dashboard, PastResult, StrategySummary, TemplateKey } from '../types'
import { templateDetails } from './data'

// MOCK: the user's strategies and what waits for them, as in the Home wireframe.
// Dates are relative to today so "started today" stays true.

const daysAgo = (n: number) => {
  const d = new Date()
  d.setDate(d.getDate() - n)
  return isoDate(d)
}

function template(key: TemplateKey) {
  const t = templateDetails.find((d) => d.key === key)
  if (!t) throw new Error(`Unknown template ${key}`)
  return t
}

function pastResult(key: TemplateKey): PastResult | null {
  const { backtest } = template(key)
  const all = backtest?.periods.at(-1)
  return backtest && all ? { years: backtest.years, template: all.template, index: all.index } : null
}

type Seed = Pick<StrategySummary, 'id' | 'name' | 'mode' | 'startedOn' | 'budget' | 'used' | 'results'> & {
  templateKey: TemplateKey
}

const seeds: Seed[] = [
  {
    id: 'zscaler-and-friends',
    name: 'Zscaler & friends',
    templateKey: 'dip',
    mode: 'live',
    startedOn: daysAgo(4),
    budget: 3000,
    used: 1200,
    results: { strategy: 6.4, index: 2.1, history: [8, 10, 7, 14, 16, 13, 22, 26, 30] },
  },
  { id: 'ride-the-long-trend', name: 'Ride the long trend', templateKey: 'trend', mode: 'simulation', startedOn: daysAgo(0), budget: 3000, used: 0, results: null },
  { id: 'my-ai-picks', name: 'My AI picks', templateKey: 'ai', mode: 'simulation', startedOn: daysAgo(4), budget: 3000, used: 0, results: null },
]

export function dashboard(): Dashboard {
  return {
    today: daysAgo(0),
    strategies: seeds.map(({ templateKey, ...s }) => ({
      ...s,
      templateKey,
      templateName: template(templateKey).name,
      pastResult: pastResult(templateKey),
    })),
    waiting: [
      { id: 'w1', kind: 'buySignal', strategyId: 'zscaler-and-friends', strategyName: 'Zscaler & friends', stockName: 'Zscaler' },
      { id: 'w2', kind: 'aiRound', strategyId: 'my-ai-picks', strategyName: 'My AI picks' },
    ],
  }
}
