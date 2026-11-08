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

/** Spain's zone is named after Madrid; we show Barcelona, our home city (Ernest). The stored value stays `Europe/Madrid`. */
const CITY_NAMES: Record<string, string> = { 'Europe/Madrid': 'Europe/Barcelona' }

/** "+1" style offset now ("GMT+1"), or nothing where the browser can't tell. */
function offset(zone: string, now: Date): string {
  try {
    const part = new Intl.DateTimeFormat('en', { timeZone: zone, timeZoneName: 'shortOffset' }).formatToParts(now).find((p) => p.type === 'timeZoneName')
    return part ? ` (${part.value})` : ''
  } catch {
    return ''
  }
}

/** One zone's name for a list: "Europe/Barcelona (GMT+1)", "America/New York (GMT-5)". */
export function timeZoneLabel(zone: string, now = new Date()): string {
  return (CITY_NAMES[zone] ?? zone).replaceAll('_', ' ') + offset(zone, now)
}

/** The time zones as select options, sorted by their shown name, always including `current`. */
export function timeZoneOptions(current: string): { value: string; label: string }[] {
  const now = new Date()
  return timeZones(current)
    .map((z) => ({ value: z, label: timeZoneLabel(z, now) }))
    .sort((a, b) => a.label.localeCompare(b.label))
}
