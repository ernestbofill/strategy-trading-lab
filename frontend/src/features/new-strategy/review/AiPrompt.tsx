import type { AiSource } from '../NewStrategyDraft'
import { AI_SOURCES } from './aiSources'
import styles from './AiPrompt.module.css'

/**
 * The suggested prompt (v1), read-only. It never includes anything about the user.
 * Criteria per source are still to be written (spec: "[criteria]").
 */
export function AiPrompt({ sources }: { sources: Record<AiSource, boolean> }) {
  const on = AI_SOURCES.filter((s) => sources[s.id])
  const n = (text: string) => <span className={styles.promptNumber}>{text}</span>
  return (
    <div className={`well ${styles.prompt}`} aria-label="Suggested prompt">
      <p className={styles.promptComment}>// suggested prompt · v1</p>
      <p>
        Today is {n('[date]')}. Search the web for news and data from the last {n('2 weeks')}, plus each company's
        latest quarterly figures.
      </p>
      <p>
        Score each stock below from {n('0')} to {n('100')} on:
      </p>
      {on.map((s) => (
        <p key={s.id} className={styles.promptSource}>
          · {s.title}: <span className={styles.promptString}>[criteria]</span>
        </p>
      ))}
      <p>and overall. Give a one-line reason and your main sources, with dates.</p>
      <p>
        Answer only in this table:{' '}
        <span className={styles.promptTable}>| Stock | {on.map((s) => s.title).join(' | ')} | Overall | Reason |</span>
      </p>
      <p>
        Stocks: <span className={styles.promptString}>[your stocks to watch]</span>
      </p>
    </div>
  )
}
