import type { StrategyMode } from './strategies'

export interface SummaryWaitingSignal {
  signalId: string
  strategyId: string
  strategyName: string
  stockName: string
  side: 'buy' | 'sell'
  signalDate: string
  stepNo: number | null
  maxSteps: number | null
  closePrice: number
}

export interface SummaryWaitingAiRound {
  strategyId: string
  strategyName: string
}

export interface SummaryChangedSignal {
  signalId: string
  strategyId: string
  strategyName: string
  mode: StrategyMode
  stockName: string
  side: 'buy' | 'sell'
  signalDate: string
  closePrice: number
  stepNo: number | null
  maxSteps: number | null
  text: string
}

export interface SummaryStockChange {
  strategyId: string
  assetId: string
  strategyName: string
  mode: StrategyMode
  stockName: string
  kind: 'becameCandidate' | 'noLongerCandidate' | 'cameClose'
  distancePct: number | null
  text: string
}

export interface SummaryStrategyScore {
  strategyId: string
  strategyName: string
  mode: StrategyMode
  startedOn: string
  signalsCount: number
}

/** The daily summary: backend Notifications/DailySummaryDtos.cs (DailySummaryDto). */
export interface DailySummary {
  date: string
  hasChanges: boolean
  waitingSignals: SummaryWaitingSignal[]
  waitingAiRounds: SummaryWaitingAiRound[]
  whatChanged: SummaryChangedSignal[]
  stockChanges: SummaryStockChange[]
  strategies: SummaryStrategyScore[]
}
