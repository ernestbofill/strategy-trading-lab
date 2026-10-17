import { useId, useState } from 'react'
import styles from './StepperCard.module.css'

interface StepperCardProps {
  label: string
  value: number
  /** The value as shown when not being edited: "€3,000", "5". */
  display: string
  /** Called with the typed number; the parent keeps it within its limits. */
  onChange: (value: number) => void
  help: string
  onDecrease: () => void
  onIncrease: () => void
  canDecrease: boolean
  canIncrease: boolean
}

/** A setting with a big value and − / + buttons, each at least 44 × 44px. */
export function StepperCard({ label, value, display, onChange, help, onDecrease, onIncrease, canDecrease, canIncrease }: StepperCardProps) {
  const id = useId()
  // While typing, the raw text is kept; it is committed on blur or Enter.
  const [text, setText] = useState<string | null>(null)

  const commit = () => {
    const digits = (text ?? '').replace(/\D/g, '')
    if (digits) onChange(Number(digits))
    setText(null)
  }

  return (
    <div className={styles.card} role="group" aria-labelledby={id}>
      <div id={id} className={`ui-strong ${styles.label}`}>
        {label}
      </div>
      <div className={styles.row}>
        <input
          className={styles.value}
          type="text"
          inputMode="numeric"
          aria-labelledby={id}
          value={text ?? display}
          onFocus={(e) => {
            setText(String(value))
            e.target.select()
          }}
          onChange={(e) => setText(e.target.value)}
          onBlur={commit}
          onKeyDown={(e) => e.key === 'Enter' && e.currentTarget.blur()}
        />
        <div className={styles.buttons}>
          <button type="button" onClick={onDecrease} disabled={!canDecrease} aria-label={`Decrease ${label.toLowerCase()}`}>
            −
          </button>
          <button type="button" onClick={onIncrease} disabled={!canIncrease} aria-label={`Increase ${label.toLowerCase()}`}>
            +
          </button>
        </div>
      </div>
      <div className={styles.help}>{help}</div>
    </div>
  )
}
