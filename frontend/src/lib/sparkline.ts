/** SVG polyline points for a small line chart, oldest value first. A flat series runs through the middle. */
export function sparklinePoints(values: number[], width: number, height: number, pad = 2): string {
  if (values.length < 2) return ''
  const min = Math.min(...values)
  const range = Math.max(...values) - min
  const step = width / (values.length - 1)
  return values
    .map((v, i) => {
      const y = range === 0 ? height / 2 : height - pad - ((v - min) / range) * (height - 2 * pad)
      return `${+(i * step).toFixed(1)},${+y.toFixed(1)}`
    })
    .join(' ')
}
