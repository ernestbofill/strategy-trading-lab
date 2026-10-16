const eurFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 })

/** €3,000 — budgets, steps and results are in euro. */
export const eur = (n: number) => `€${eurFormat.format(n)}`

/** +12% / −8% with a true minus sign, so the sign carries the meaning, not only the colour. */
export const signedPct = (n: number) => `${n > 0 ? '+' : n < 0 ? '−' : ''}${Math.abs(n)}%`
