/** SVG polyline points for a small line chart, oldest value first. A flat series runs through the middle. */
export const sparklinePoints = (values: number[], width: number, height: number, pad = 2): string =>
  chartLines([values], width, height, pad)[0]

/**
 * SVG polyline points for several lines on one shared scale, so they can be compared: one string per series, in order.
 * A null value leaves that day out of its line.
 */
export function chartLines(series: (number | null)[][], width: number, height: number, pad = 2): string[] {
  const length = Math.max(0, ...series.map((s) => s.length))
  const known = series.flat().filter((v): v is number => v !== null)
  if (length < 2 || known.length === 0) return series.map(() => '')
  const min = Math.min(...known)
  const range = Math.max(...known) - min
  const step = width / (length - 1)
  return series.map((values) =>
    values
      .map((v, i) => {
        if (v === null) return null
        const y = range === 0 ? height / 2 : height - pad - ((v - min) / range) * (height - 2 * pad)
        return `${+(i * step).toFixed(1)},${+y.toFixed(1)}`
      })
      .filter((p) => p !== null)
      .join(' '),
  )
}
