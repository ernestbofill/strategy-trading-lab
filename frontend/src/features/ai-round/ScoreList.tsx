import type { AiScore } from '../../api'
import { useT } from '../../i18n/I18n'
import styles from './AiRound.module.css'

/** "Your AI's scores": one row per stock, top score first, with the AI's one-line reason; a stock that gets a step stands out. */
export function ScoreList({ scores, maxSteps }: { scores: AiScore[]; maxSteps: number | null }) {
  const t = useT()
  const sorted = [...scores].sort((a, b) => b.overall - a.overall || a.name.localeCompare(b.name))
  return (
    <ol className={styles.scores} aria-label={t('aiRound.scores.aria')}>
      {sorted.map((s) => (
        <li key={s.assetId} className={styles.score} data-picked={s.step !== null || undefined}>
          <span className={styles.overall}>
            {s.overall}
            <span className="visually-hidden">{t('aiRound.scores.outOf')}</span>
          </span>
          <span className={styles.scoreName}>
            {s.name}
            {s.step !== null && (
              <span className="ui-strong">
                {t('aiRound.scores.step', { step: maxSteps === null ? s.step : t('signal.stepOf', { n: s.step, max: maxSteps }) })}
              </span>
            )}
          </span>
          <span className={styles.reason}>{s.reason || '–'}</span>
        </li>
      ))}
    </ol>
  )
}
