import styles from './ToggleTiles.module.css'

interface ToggleTilesProps<Id extends string> {
  tiles: { id: Id; title: string; detail: string }[]
  isOn: (id: Id) => boolean
  onToggle: (id: Id) => void
  /** When set, the last tile that is on can't be turned off. */
  keepOne?: boolean
}

/** Row of on/off tiles (aria-pressed). On shows ✓ and a highlight, off shows +. */
export function ToggleTiles<Id extends string>({ tiles, isOn, onToggle, keepOne }: ToggleTilesProps<Id>) {
  const onCount = tiles.filter((t) => isOn(t.id)).length
  return (
    <div className={styles.tiles}>
      {tiles.map((t) => {
        const on = isOn(t.id)
        const locked = keepOne && on && onCount === 1
        return (
          <button
            key={t.id}
            type="button"
            aria-pressed={on}
            aria-disabled={locked || undefined}
            onClick={() => !locked && onToggle(t.id)}
            className={styles.tile}
          >
            <span className={`ui-strong ${styles.title}`}>
              <span aria-hidden="true">{on ? '✓' : '+'}</span> {t.title}
            </span>
            <span className={styles.detail}>{t.detail}</span>
          </button>
        )
      })}
    </div>
  )
}
