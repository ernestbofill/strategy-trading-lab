import { Link } from 'react-router-dom'
import type { StrategySummary } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { pickPath } from '../new-strategy/paths'
import { useTemplates } from '../new-strategy/useTemplate'
import { useCurrentAccount } from '../account/AccountContext'
import { VerifyNotice } from '../account/VerifyNotice'
import { SEND_FEEDBACK_HASH } from '../feedback/SendFeedback'
import { accountPath } from './paths'
import styles from './Dashboard.module.css'
import { StrategyCard } from './StrategyCard'
import { useDashboard } from './useDashboard'
import { WaitingList } from './WaitingList'

/** Dashboard (wireframe `Home`). */
export function Dashboard() {
  const t = useT()
  const dashboard = useDashboard()
  const { emailVerified } = useCurrentAccount()
  // Loads the template list in the background, so Pick a template opens at once.
  useTemplates()
  const top = <TopBar title={t('dashboard.title')} aside={<TextAction to={accountPath}>{t('dashboard.account')}</TextAction>} />

  return (
    <LoadableScreen state={dashboard} top={top} what={t('dashboard.what')}>
      {({ today, strategies, waiting }) => {
        // The API sends the running ones first, then the ended ones, the archived ones last.
        const card = (s: StrategySummary) => (
          <StrategyCard key={s.id} strategy={s} today={today} waiting={waiting.find((w) => w.strategyId === s.id)} />
        )
        const ended = strategies.filter((s) => s.status === 'ended' && !s.archived)
        const archived = strategies.filter((s) => s.archived)
        return (
          <Screen top={top} note={`${t('common.disclaimer')} ${t('common.pastResults')}`}>
            <VerifyNotice />
            <PageHeader title={t('dashboard.heading')} />
            <WaitingList items={waiting} />
            <div className={styles.cards}>
              {strategies.filter((s) => s.status === 'running').map(card)}
              <Link to={pickPath} className={`ui-strong ${styles.add}`}>
                {t('dashboard.newStrategy')}
              </Link>
            </div>
            {ended.length > 0 && <div className={styles.cards}>{ended.map(card)}</div>}
            {archived.length > 0 && (
              <details className={styles.archived}>
                <summary className="ui-strong">{t('dashboard.archived', { n: archived.length })}</summary>
                <div className={styles.cards}>{archived.map(card)}</div>
              </details>
            )}
            {emailVerified && (
              <p className={styles.feedback}>
                <TextAction to={`${accountPath}${SEND_FEEDBACK_HASH}`}>{t('feedback.send.title')}</TextAction>
              </p>
            )}
          </Screen>
        )
      }}
    </LoadableScreen>
  )
}
