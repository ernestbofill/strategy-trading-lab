export const strategyPath = (id: string) => `/strategies/${id}`
/** Confirm a signal: one signal of a strategy. */
export const signalPath = (strategyId: string, signalId: string) => `/strategies/${strategyId}/signals/${signalId}`
export const aiRoundPath = (id: string) => `/strategies/${id}/ai-round`
export const accountPath = '/account'
