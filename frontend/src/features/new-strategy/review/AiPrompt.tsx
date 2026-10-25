import { useT } from '../../../i18n/I18n'
import type { AiSource } from '../NewStrategyDraft'
import { AI_SOURCES } from './aiSources'
import styles from './AiPrompt.module.css'

/**
 * The suggested prompt (v1), read-only. It never includes anything about the user.
 * Criteria per source are still to be written (spec: "[criteria]").
 */
export function AiPrompt({ sources }: { sources: Record<AiSource, boolean> }) {
  const t = useT()
  const on = AI_SOURCES.filter((s) => sources[s.id])
  const n = (text: string) => <span className={styles.promptNumber}>{text}</span>
  return (
    <div className={`well ${styles.prompt}`} aria-label={t('newStrategy.prompt.aria')}>
      <p className={styles.promptComment}>{t('newStrategy.prompt.comment')}</p>
      <p>
        Today is {n(t('newStrategy.prompt.date'))}. Search the web for news and data from the last {n(t('newStrategy.prompt.period'))}, plus each company's
        latest quarterly figures.
      </p>
      <p>
        Score each stock below from {n('0')} to {n('100')} on:
      </p>
      {on.map((s) => (
        <p key={s.id} className={styles.promptSource}>
          · {s.title}: <span className={styles.promptString}>{t('newStrategy.prompt.criteria')}</span>
        </p>
      ))}
      <p>{t('newStrategy.prompt.overall')}</p>
      <p>
        {t('newStrategy.prompt.answer')}{' '}
        <span className={styles.promptTable}>
          {t('newStrategy.prompt.table', { sources: on.map((s) => s.title).join(' | ') })}
        </span>
      </p>
      <p>
        {t('newStrategy.prompt.stocks')} <span className={styles.promptString}>{t('newStrategy.prompt.yourStocks')}</span>
      </p>
    </div>
  )
}
