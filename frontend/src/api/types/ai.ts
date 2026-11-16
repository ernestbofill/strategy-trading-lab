import type { Currency } from './account'
import type { StrategyMode } from './strategies'

/** What the AI prompt asks for: the sources on, and the user's own instructions (null for the suggested ones). */
export interface AiSetup {
  news: boolean
  mood: boolean
  figures: boolean
  instructions: string | null
}

/** The prompt for the review and the editor, before a strategy exists. */
export interface AiPromptRequest extends AiSetup {
  everyDays: number
  /** The ticked stocks. */
  assetIds: string[]
}

export interface AiPromptPreview {
  version: number
  text: string
  /** The suggested instructions for these sources: what "Reset to suggested" puts back. */
  suggested: string
}

export interface AiStock {
  assetId: string
  symbol: string
  name: string
}

/** The AI round screen: backend Ai/AiRoundDtos.cs (AiRoundDetail). */
export interface AiRoundDetail {
  strategyId: string
  strategyName: string
  templateName: string
  mode: StrategyMode
  /** The strategy's own currency. */
  currency: Currency
  roundId: string
  dueOn: string
  /** The last day it can be run; after it the round is skipped. */
  lastDay: string
  /** When the round after it is due, once it is run or skipped. */
  nextDueOn: string
  /** Whether it can be run today. Before that, an extra round can be (the schedule stays). */
  open: boolean
  today: string
  promptVersion: number
  promptEdited: boolean
  prompt: string
  setup: AiSetup
  suggested: string
  stocks: AiStock[]
  everyDays: number
  topN: number
  lastModel: string | null
  sharing: boolean
  /** Whether the user has answered the sharing question (the consent box shows until then). */
  sharingAsked: boolean
}

/** A stock's scores as read from the answer, with the step it gets (or got). */
export interface AiScore {
  assetId: string
  symbol: string
  name: string
  overall: number
  news: number | null
  mood: number | null
  figures: number | null
  reason: string
  step: number | null
}

/** What was read from a pasted answer. `short`: why fewer stocks than the top N get a step. */
export interface AiReading {
  scores: AiScore[]
  problems: string[]
  /** A stock without a score or one that isn't yours: shown, but the scores can still be used. */
  warnings: string[]
  ok: boolean
  maxSteps: number | null
  stepAmount: number | null
  short: 'budget' | 'allSteps' | 'unavailable' | null
}

export interface AiRoundResult {
  roundId: string
  signalIds: string[]
  mode: StrategyMode
}

/** The picks' and the rest's average price change since their rounds, in percent. */
export interface PicksVsRest {
  picks: number
  rest: number
  rounds: number
}

export interface AiRoundItem {
  id: string
  dueOn: string
  /** Run before the scheduled round was due: dated the day it was run. */
  extra: boolean
  status: 'due' | 'done' | 'skipped'
  doneAt: string | null
  sessionDate: string | null
  aiModel: string | null
  promptVersion: number | null
  promptEdited: boolean
  prompt: string | null
  answer: string | null
  scores: AiScore[]
  picks: number | null
  rest: number | null
}

export interface AiRoundHistory {
  strategyId: string
  strategyName: string
  mode: StrategyMode
  /** The user's local date. */
  today: string
  rounds: AiRoundItem[]
  picksVsRest: PicksVsRest | null
}

/** On the strategy page: the next round (due now or later) and the picks against the rest. */
export interface AiRoundsSummary {
  nextDueOn: string | null
  due: boolean
  done: number
  skipped: number
  picksVsRest: PicksVsRest | null
}
