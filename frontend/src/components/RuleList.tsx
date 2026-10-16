import type { Rule } from '../api'
import { RichText } from './RichText'
import styles from './RuleList.module.css'

/** "It signals when": numbered (01, 02…) or labelled (BUY, SELL…) rule lines in a deep well. */
export function RuleList({ rules }: { rules: Rule[] }) {
  return (
    <ol className={`well ${styles.list}`}>
      {rules.map((r) => (
        <li key={r.marker} className={styles.rule}>
          <span className={styles.marker} data-tone={r.tone}>
            {r.marker}
          </span>
          <span>
            <RichText text={r.text} />
          </span>
        </li>
      ))}
    </ol>
  )
}
