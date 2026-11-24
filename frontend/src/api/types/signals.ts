import type { Currency } from './account'
import type { TradeSide } from './strategies'

export type EnteredAs = 'amount' | 'shares'

export type FxSource = 'reference' | 'user'

export type TradeSource = 'signal' | 'manual'

export interface RateDay {
  date: string
  rate: number
}

export interface CurrencyRates {
  currency: string
  rates: RateDay[]
}

export interface StrategyTradeItem {
  id: string
  assetId: string
  symbol: string
  stockName: string
  tradeDate: string
  side: TradeSide
  price: number
  assetCurrency: string
  enteredAs: EnteredAs
  amount: number
  shares: number
  fee: number
  strategyCurrency: Currency
  fxRate: number
  fxSource: FxSource
  source: TradeSource
  signalId: string | null
}

export interface StrategySignalItem {
  id: string
  assetId: string
  symbol: string
  stockName: string
  signalDate: string
  side: TradeSide
  stepNo: number | null
  maxSteps: number | null
  stepAmount: number | null
  closePrice: number
  assetCurrency: string
  reason: string
  status: SignalStatus
  skipReason: SkipReason | null
  snoozedUntil: string | null
  endedOn: string | null
  createdAt: string
}

export interface ManualTradeRequest {
  assetId: string
  side: TradeSide
  date: string | null
  price: number | null
  enteredAs: EnteredAs | null
  amount: number | null
  shares: number | null
  fee: number | null
  fxSource: FxSource | null
  fxRate: number | null
}

export interface UpdateTradeRequest {
  date: string | null
  price: number | null
  enteredAs: EnteredAs | null
  amount: number | null
  shares: number | null
  fee: number | null
  fxSource: FxSource | null
  fxRate: number | null
}

export type SignalStatus = 'waiting' | 'confirmed' | 'skipped' | 'missed'

export type SkipReason = 'priceRanAway' | 'didntTrustIt' | 'noCash' | 'other'

/** How the user answered a signal: the trade they saved, or the skip. */
export interface SignalAnswer {
  status: SignalStatus
  skipReason: SkipReason | null
  date: string | null
  price: number | null
  amount: number | null
  shares: number | null
  fee: number | null
}

/** Confirm a signal: backend Signals/SignalDtos.cs (SignalDetail). Prices are in the asset's currency, amounts in the strategy's. */
export interface SignalDetail {
  id: string
  strategyId: string
  strategyName: string
  templateName: string
  strategyCurrency: Currency
  stockName: string
  /** ISO code; may be one the user can't pick ("JPY"). */
  assetCurrency: string
  stepNo: number | null
  maxSteps: number | null
  stepAmount: number | null
  signalDate: string
  closePrice: number
  /** What the rules found: "8.2% below its recent peak, still above its 50-day average". */
  reason: string
  status: SignalStatus
  snoozedUntil: string | null
  endedOn: string | null
  /** The user's local date. */
  today: string
  /**
   * The next trading day, its opening price once stored (else, once that session has opened, the latest price with
   * `priceIsLatest`, or else the signal day's close, with `priceIsClose`), the step without
   * the tested fee, and held shares for a sale.
   */
  prefill: {
    date: string
    price: number | null
    amount: number | null
    shares?: number | null
    priceIsClose?: boolean
    priceIsLatest?: boolean
  }
  /** Reference rates by day: asset currency per one unit of the strategy's. */
  rates: { date: string; rate: number }[]
  answer: SignalAnswer | null
  side?: TradeSide
  canSnooze?: boolean
  assetId?: string
  /** The strategy follows a list of stocks: the stock has its own page, the signal's parent. */
  onStockList?: boolean
  /** A missed signal whose stock got a newer signal of the same side: that one takes the answer. */
  replacedBy?: string | null
}

/** "I did it". `fxRate` is only read when `fxSource` is 'user'. */
export interface SignalTrade {
  date: string | null
  price: number
  enteredAs: 'amount' | 'shares'
  amount: number | null
  shares: number | null
  fee: number | null
  fxSource: 'reference' | 'user'
  fxRate: number | null
}
