import { sparklinePoints } from '../lib/sparkline'

const WIDTH = 110
const HEIGHT = 40

/** Small decorative line chart; the figure next to it says the same in words. Takes its colour from the text. */
export function Sparkline({ values }: { values: number[] }) {
  return (
    <svg width={WIDTH} height={HEIGHT} viewBox={`0 0 ${WIDTH} ${HEIGHT}`} aria-hidden="true" style={{ flex: 'none' }}>
      <polyline
        points={sparklinePoints(values, WIDTH, HEIGHT)}
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinejoin="round"
      />
    </svg>
  )
}
