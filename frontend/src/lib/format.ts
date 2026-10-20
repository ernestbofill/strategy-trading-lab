const eurFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 })

/** €3,000 — budgets, steps and results are in euro. */
export const eur = (n: number) => `€${eurFormat.format(n)}`

/** +12% / −8% with a true minus sign, so the sign carries the meaning, not only the colour. */
export const signedPct = (n: number, digits?: number) => {
  const abs = Math.abs(n)
  return `${n > 0 ? '+' : n < 0 ? '−' : ''}${digits === undefined ? abs : abs.toFixed(digits)}%`
}

/** Local calendar date as "2026-10-13". */
export const isoDate = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

const shortDateFormat = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' })

/** "Oct 13", from a local date like "2026-10-13". */
export const shortDate = (iso: string) => {
  const [y, m, d] = iso.split('-').map(Number)
  return shortDateFormat.format(new Date(y, m - 1, d))
}
