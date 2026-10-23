/** The browser's time zone, the default on Sign-up ("Europe/Madrid"); UTC when it can't tell. */
export function guessTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
  } catch {
    return 'UTC'
  }
}

/** The time zones to pick from, always including `current` (a browser may list fewer than it reports). */
export function timeZones(current: string): string[] {
  let all: string[] = []
  try {
    all = Intl.supportedValuesOf('timeZone')
  } catch {
    // Older browsers: only the guessed one, which the API checks.
  }
  return all.includes(current) ? all : [current, ...all]
}
