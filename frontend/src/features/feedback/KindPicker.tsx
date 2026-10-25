import type { FeedbackKind } from '../../api'
import { useT } from '../../i18n/I18n'
import { FEEDBACK_KINDS } from './feedback'
import styles from './Feedback.module.css'

export function KindPicker({
  value,
  onChange,
}: {
  value: FeedbackKind
  onChange: (kind: FeedbackKind) => void
}) {
  const t = useT()
  return (
    <div className={styles.kindGroup} role="radiogroup" aria-label={t('feedback.kind.type')}>
      {FEEDBACK_KINDS.map((k) => (
        <button
          key={k.value}
          type="button"
          role="radio"
          aria-checked={value === k.value}
          className={styles.kindOption}
          onClick={() => onChange(k.value)}
        >
          <span aria-hidden="true">{k.emoji}</span> {t(`feedback.kind.${k.value}`)}
        </button>
      ))}
    </div>
  )
}
