/** The browser's time zone, the default on Sign-up ("Europe/Madrid"); UTC when it can't tell. */
export function guessTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
  } catch {
    return 'UTC'
  }
}

/**
 * The short list to pick from (Ernest, 2026-11-08): one row per clock, named after a few known cities, as most apps do,
 * instead of every IANA zone. Cities that keep the same time all year share a row (Paris and Copenhagen are Barcelona's).
 * Spain's row names Barcelona, Paris and Copenhagen, Ernest's favourites; the stored value is the IANA name, which the API checks.
 */
const ZONES: [zone: string, cities: string][] = [
  ['Pacific/Honolulu', 'Honolulu'],
  ['America/Anchorage', 'Anchorage'],
  ['America/Los_Angeles', 'Los Angeles, San Francisco, Vancouver'],
  ['America/Phoenix', 'Phoenix'],
  ['America/Denver', 'Denver, Calgary'],
  ['America/Mexico_City', 'Mexico City'],
  ['America/Chicago', 'Chicago, Dallas, Winnipeg'],
  ['America/Bogota', 'Bogotá, Lima, Quito'],
  ['America/New_York', 'New York, Toronto, Miami'],
  ['America/Caracas', 'Caracas, La Paz'],
  ['America/Halifax', 'Halifax'],
  ['America/Santiago', 'Santiago'],
  ['America/St_Johns', "St. John's"],
  ['America/Sao_Paulo', 'São Paulo, Buenos Aires, Montevideo'],
  ['Atlantic/Azores', 'Azores'],
  ['UTC', 'UTC'],
  ['Europe/London', 'London, Lisbon, Dublin, Canary Islands'],
  ['Africa/Lagos', 'Lagos, Algiers'],
  ['Europe/Madrid', 'Barcelona, Paris, Copenhagen'],
  ['Africa/Johannesburg', 'Johannesburg'],
  ['Africa/Cairo', 'Cairo'],
  ['Europe/Athens', 'Athens, Helsinki, Kyiv, Bucharest'],
  ['Asia/Jerusalem', 'Jerusalem, Tel Aviv'],
  ['Europe/Istanbul', 'Istanbul, Moscow, Riyadh, Nairobi'],
  ['Asia/Tehran', 'Tehran'],
  ['Asia/Dubai', 'Dubai, Baku'],
  ['Asia/Kabul', 'Kabul'],
  ['Asia/Karachi', 'Karachi, Tashkent'],
  ['Asia/Kolkata', 'Mumbai, Delhi, Bangalore'],
  ['Asia/Kathmandu', 'Kathmandu'],
  ['Asia/Dhaka', 'Dhaka'],
  ['Asia/Bangkok', 'Bangkok, Jakarta, Hanoi'],
  ['Asia/Singapore', 'Singapore, Hong Kong, Beijing, Perth'],
  ['Asia/Tokyo', 'Tokyo, Seoul'],
  ['Australia/Darwin', 'Darwin'],
  ['Australia/Adelaide', 'Adelaide'],
  ['Australia/Brisbane', 'Brisbane'],
  ['Australia/Sydney', 'Sydney, Melbourne, Canberra'],
  ['Pacific/Auckland', 'Auckland, Wellington'],
]

/** The offset now, as the browser writes it ("GMT+1", "GMT-3:30", "GMT"), and in minutes for sorting. */
function offsetOf(zone: string, now: Date): { text: string; minutes: number } {
  try {
    const text =
      new Intl.DateTimeFormat('en', { timeZone: zone, timeZoneName: 'shortOffset' }).formatToParts(now).find((p) => p.type === 'timeZoneName')
        ?.value ?? 'GMT'
    const [, sign, h, m] = /GMT([+-])?(\d+)?(?::(\d+))?/.exec(text) ?? []
    const minutes = (sign === '-' ? -1 : 1) * (Number(h ?? 0) * 60 + Number(m ?? 0))
    return { text, minutes }
  } catch {
    return { text: 'GMT', minutes: 0 }
  }
}

/** One row's name: "GMT+1 · Barcelona, Paris, Copenhagen"; a zone off the list by its IANA name. */
export function timeZoneLabel(zone: string, now = new Date()): string {
  const cities = ZONES.find(([z]) => z === zone)?.[1] ?? zone.replaceAll('_', ' ')
  return `${offsetOf(zone, now).text} · ${cities}`
}

/** Whether two zones keep the same clock: the same offset in winter and in summer. */
const sameClock = (a: string, b: string) =>
  [new Date('2026-01-15T12:00:00Z'), new Date('2026-07-15T12:00:00Z')].every((d) => offsetOf(a, d).minutes === offsetOf(b, d).minutes)

/**
 * The rows to pick from, west to east by their offset today. `current` (a zone the browser guessed, or one saved before this
 * list) is always offered: as the value of the row that keeps the same clock ("Europe/Paris" on Barcelona's row, so no row
 * repeats), or as a row of its own when none does.
 */
export function timeZoneOptions(current: string): { value: string; label: string }[] {
  const now = new Date()
  const listed = ZONES.some(([z]) => z === current)
  const twin = listed ? undefined : ZONES.find(([z]) => sameClock(z, current))?.[0]
  const rows = ZONES.map(([zone]) => ({ value: zone === twin ? current : zone, label: timeZoneLabel(zone, now), zone }))
  if (!listed && !twin) rows.push({ value: current, label: timeZoneLabel(current, now), zone: current })
  return rows
    .map((row) => ({ ...row, minutes: offsetOf(row.zone, now).minutes }))
    .sort((a, b) => a.minutes - b.minutes)
    .map(({ value, label }) => ({ value, label }))
}
