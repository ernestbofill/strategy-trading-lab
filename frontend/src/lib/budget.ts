import type { Budget } from '../api'

// Budget screen (4 / 5): the three settings, their limits and the numbers derived from them.

interface BudgetSettings {
  budget: number
  step: number
  maxSteps: number
}

/** The draft's settings, null where the user kept the template's tested default. */
type BudgetChoices = { [K in keyof BudgetSettings]: number | null }

/**
 * What the user set, else the template's tested defaults.
 * An all-in, all-out template has no step: the whole budget is one step.
 */
export function resolveBudget(choices: BudgetChoices, defaults: Budget): BudgetSettings {
  const budget = choices.budget ?? defaults.amount
  return {
    budget,
    step: choices.step ?? defaults.step ?? budget,
    maxSteps: choices.maxSteps ?? defaults.maxSteps ?? 1,
  }
}

export const BUDGET_LIMITS = { min: 500, max: 1_000_000, stepBy: 500 }
export const STEP_LIMITS = { min: 10, stepBy: 10 }
export const MAX_STEPS_LIMITS = { min: 1, max: 20, stepBy: 1 }

/** Squares drawn in the summary; more steps than this are cut off. */
const MAX_SQUARES = 60

export const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value))

export const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`

interface BudgetSummary {
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
