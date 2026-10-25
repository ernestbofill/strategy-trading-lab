import type { FeedbackKind } from '../../api'
import { FEEDBACK_KINDS } from './feedback'
import styles from './Feedback.module.css'

export function KindPicker({
  value,
  onChange,
}: {
  value: FeedbackKind
  onChange: (kind: FeedbackKind) => void
}) {
  return (
    <div className={styles.kindGroup} role="radiogroup" aria-label="Type">
      {FEEDBACK_KINDS.map((k) => (
        <button
          key={k.value}
          type="button"
          role="radio"
          aria-checked={value === k.value}
          className={styles.kindOption}
          onClick={() => onChange(k.value)}
        >
          <span aria-hidden="true">{k.emoji}</span> {k.label}
        </button>
      ))}
    </div>
  )
}
