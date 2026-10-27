export const strategyPath = (id: string) => `/strategies/${id}`
/** Advanced settings for a running strategy ("Tune the settings"). */
export const strategySettingsPath = (id: string) => `/strategies/${id}/settings`
/** Confirm a signal: one signal of a strategy. */
export const signalPath = (strategyId: string, signalId: string) => `/strategies/${strategyId}/signals/${signalId}`
export const aiRoundPath = (id: string) => `/strategies/${id}/ai-round`
export const accountPath = '/account'
/** A strategy's AI round history. */
export const aiRoundsPath = (id: string) => `/strategies/${id}/ai-rounds`
/** The prompt editor of a running AI strategy. */
export const aiPromptPath = (id: string) => `/strategies/${id}/prompt`
