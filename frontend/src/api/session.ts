// The app learns here that its session has ended (a 401 from a screen's data), so it can show Log-in.

const listeners = new Set<() => void>()

/** Calls `listener` whenever a request finds the session gone. Returns the unsubscribe. */
export function onSessionEnded(listener: () => void): () => void {
  listeners.add(listener)
  return () => void listeners.delete(listener)
}

export function sessionEnded() {
  for (const listener of listeners) listener()
}
