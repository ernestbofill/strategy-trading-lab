// DTOs shared with the backend. Shapes mirror the future .NET API (camelCase JSON).

export type TemplateKey = 'dip' | 'comeback' | 'drop' | 'trend' | 'ai'

export type InvestsIn = 'stockList' | 'sp500Fund' | 'indexFund'

export type SignalSides = 'buysOnly' | 'buysAndSells'

export interface StrategyTemplate {
  key: TemplateKey
  /** Rule set the engine runs, as named in the POC (null for new templates). */
  engineKey: string | null
  name: string
  tagline: string
  idea: string
  investsIn: InvestsIn
  signals: SignalSides
  /** Whether a backtest exists (Your AI's top picks has none). */
  hasBacktest: boolean
}

export interface Api {
  getTemplates(): Promise<StrategyTemplate[]>
  getTemplate(key: TemplateKey): Promise<StrategyTemplate | undefined>
}
