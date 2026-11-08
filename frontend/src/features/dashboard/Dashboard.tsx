import { Link } from 'react-router-dom'
import type { StrategySummary } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { MAX_RUNNING_STRATEGIES } from '../new-strategy/draftState'
import { useNewStrategyDraft } from '../new-strategy/NewStrategyDraft'
import { pickPath } from '../new-strategy/paths'
import { welcomePath } from '../welcome/paths'
import { useCurrentAccount } from '../account/AccountContext'
import { VerifyNotice } from '../account/VerifyNotice'
import { SEND_FEEDBACK_HASH } from '../feedback/SendFeedback'
import { settingsPath } from './paths'
import styles from './Dashboard.module.css'
import { StrategyCard } from './StrategyCard'
import { useDashboard } from './useDashboard'
import { WaitingList } from './WaitingList'

/** Dashboard (wireframe `Home`). */
export function Dashboard() {
  const t = useT()
  const dashboard = useDashboard()
  const { emailVerified } = useCurrentAccount()
  const draft = useNewStrategyDraft()
  const top = <TopBar title={t('dashboard.title')} titleLink={welcomePath} />

  return (
    <LoadableScreen state={dashboard} top={top} what={t('dashboard.what')}>
      {({ today, strategies, waiting }) => {
        // The API sends the running ones first, then the archived ones, last archived first.
        const card = (s: StrategySummary) => (
          <StrategyCard key={s.id} strategy={s} today={today} waiting={waiting.find((w) => w.strategyId === s.id)} />
        )
        const running = strategies.filter((s) => s.status === 'running')
        const archived = strategies.filter((s) => s.status === 'ended')
        return (
          <Screen top={top}>
            <VerifyNotice />
            <PageHeader title={t('dashboard.heading')} />
            <WaitingList items={waiting} />
            <div className={styles.cards}>
              {running.map(card)}
              {/* At 10 running strategies, the button is off (spec: Dashboard). */}
              {running.length >= MAX_RUNNING_STRATEGIES ? (
                <button type="button" className={`ui-strong ${styles.add}`} disabled>
                  {t('dashboard.strategiesFull', { max: MAX_RUNNING_STRATEGIES })}
                </button>
              ) : (
                <Link to={pickPath} className={`ui-strong ${styles.add}`} onClick={() => draft.sourceStrategyId && draft.reset()}>
                  {t('dashboard.newStrategy')}
                </Link>
              )}
            </div>
            {archived.length > 0 && (
              <details className={styles.archived}>
                <summary className="ui-strong">{t('dashboard.archived', { n: archived.length })}</summary>
                <div className={styles.cards}>{archived.map(card)}</div>
              </details>
            )}
            {emailVerified && (
              <p className={styles.feedback}>
                <TextAction to={`${settingsPath}${SEND_FEEDBACK_HASH}`}>{t('feedback.send.title')}</TextAction>
              </p>
            )}
          </Screen>
        )
      }}
    </LoadableScreen>
  )
}
