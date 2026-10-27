import { useT } from '../../i18n/I18n'
import styles from './AiRound.module.css'

/** "What we keep": what an anonymously shared AI round holds, and what it never holds. */
export function AiSharingDetails() {
  const t = useT()
  return (
    <details className={styles.details}>
      <summary className="ui-strong">{t('aiRound.keep.title')}</summary>
      <p className="caption">{t('aiRound.keep.what')}</p>
      <p className="caption">{t('aiRound.keep.never')}</p>
    </details>
  )
}
