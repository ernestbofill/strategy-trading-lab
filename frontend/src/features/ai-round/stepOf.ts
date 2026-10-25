import type { Translate } from '../../i18n/I18n'

/** "2 of 5", or just "2" when the budget has no step count. */
export const stepOf = (t: Translate, step: number, maxSteps: number | null) =>
  maxSteps === null ? String(step) : t('signal.stepOf', { n: step, max: maxSteps })
