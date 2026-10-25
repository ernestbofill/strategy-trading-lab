import type { AiScore } from '../../api'
import styles from './AiRound.module.css'

/** "Your AI's scores": one row per stock, top score first, with the AI's one-line reason; a stock that gets a step stands out. */
export function ScoreList({ scores, maxSteps }: { scores: AiScore[]; maxSteps: number | null }) {
  const sorted = [...scores].sort((a, b) => b.overall - a.overall || a.name.localeCompare(b.name))
  return (
    <ol className={styles.scores} aria-label="Your AI's scores">
      {sorted.map((s) => (
        <li key={s.assetId} className={styles.score} data-picked={s.step !== null || undefined}>
          <span className={styles.overall}>
            {s.overall}
            <span className="visually-hidden"> out of 100</span>
          </span>
          <span className={styles.scoreName}>
            {s.name}
            {s.step !== null && (
              <span className="ui-strong">
                · Step {s.step}
                {maxSteps === null ? '' : ` of ${maxSteps}`}
              </span>
            )}
          </span>
          <span className={styles.reason}>{s.reason || '–'}</span>
        </li>
      ))}
    </ol>
  )
}
