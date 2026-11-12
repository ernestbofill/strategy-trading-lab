const newYork = new Intl.DateTimeFormat('en-US', {
  timeZone: 'America/New_York',
  weekday: 'short',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
})

/**
 * Whether a US session is in progress (a weekday, 09:30–16:30 New York), as the API's `UsSession.InProgress` reads it:
 * a stock added then is checked after the close, not at once.
 */
export function usSessionInProgress(now: Date = new Date()): boolean {
  const parts = Object.fromEntries(newYork.formatToParts(now).map((p) => [p.type, p.value]))
  if (parts.weekday === 'Sat' || parts.weekday === 'Sun') return false
  const minutes = Number(parts.hour) * 60 + Number(parts.minute)
  return minutes >= 9 * 60 + 30 && minutes < 16 * 60 + 30
}
