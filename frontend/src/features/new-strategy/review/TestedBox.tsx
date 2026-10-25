import { Link } from 'react-router-dom'
import type { Backtest } from '../../../api'
import { Section } from '../../../components/Section'
import { useT } from '../../../i18n/I18n'
import { ResultFigures } from './Results'
import styles from './TestedBox.module.css'

/** "Tested" section on a review: scope, the whole-period figures, caveat and the How we tested link. */
export function TestedBox({ backtest, howTestedTo }: { backtest: Backtest; howTestedTo: string }) {
  const t = useT()
  return (
    <Section title={t('newStrategy.tested.title')}>
      <div className={`well ${styles.box}`}>
        <p className={styles.scope}>{backtest.scope}</p>
        <ResultFigures result={backtest.periods[backtest.periods.length - 1]} />
        <p className="caption">{t('common.pastResults')}</p>
        <Link to={howTestedTo} className={styles.link}>
          {t('newStrategy.tested.howLink')}
        </Link>
      </div>
    </Section>
  )
}
