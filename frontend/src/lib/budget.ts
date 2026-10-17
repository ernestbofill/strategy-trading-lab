// Budget screen (4 / 5): the three settings, their limits and the numbers derived from them.

export const DEFAULT_BUDGET = { budget: 3000, step: 100, maxSteps: 5 }

export const BUDGET_LIMITS = { min: 500, max: 1_000_000, stepBy: 500 }
export const STEP_LIMITS = { min: 10, stepBy: 10 }
export const MAX_STEPS_LIMITS = { min: 1, max: 20, stepBy: 1 }

/** Squares drawn in the summary; more steps than this are cut off. */
export const MAX_SQUARES = 60

export const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value))

export const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`

export interface BudgetSummary {
  /** Euro per stock when bought in full. */
  perStock: number
  totalSteps: number
  fullStocks: number
  /** Squares to draw, and how many steps there are in all. */
  shown: number
}

export function summarize(budget: number, step: number, maxSteps: number): BudgetSummary {
  const totalSteps = Math.floor(budget / step)
  return {
    perStock: step * maxSteps,
    totalSteps,
    fullStocks: Math.floor(totalSteps / maxSteps),
    shown: Math.min(totalSteps, MAX_SQUARES),
  }
}

/** Weeks until an AI round's one-step-per-round pace has used the whole budget. */
export const weeksToUse = (totalSteps: number, everyWeeks: number) => totalSteps * everyWeeks
