/** Where the Backtest page opens: a template's defaults, a strategy, or one stock, and the page to go back to. */
export interface BacktestEntry {
  template?: string
  strategy?: string
  stock?: string
  /** The page the top bar goes back to. */
  from: string
}

/** What Advanced settings hands over: the settings on screen, not saved yet ("Current"). */
export interface BacktestState {
  settings?: Record<string, number>
}

export const backtestPath = ({ template, strategy, stock, from }: BacktestEntry) => {
  const query = new URLSearchParams()
  if (template) query.set('template', template)
  if (strategy) query.set('strategy', strategy)
  if (stock) query.set('stock', stock)
  query.set('from', from)
  return `/backtest?${query}`
}
