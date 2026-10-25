export const strategyPath = (id: string) => `/strategies/${id}`
/** Advanced settings for a running strategy ("Tune the settings"). */
export const strategySettingsPath = (id: string) => `/strategies/${id}/settings`
/** Confirm a signal: one signal of a strategy. */
export const signalPath = (strategyId: string, signalId: string) => `/strategies/${strategyId}/signals/${signalId}`
export const aiRoundPath = (id: string) => `/strategies/${id}/ai-round`
export const accountPath = '/account'
