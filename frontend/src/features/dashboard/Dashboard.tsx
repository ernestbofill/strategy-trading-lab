import { Link } from 'react-router-dom'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { pickPath } from '../new-strategy/paths'
import { useTemplates } from '../new-strategy/useTemplate'
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
  // Loads the template list in the background, so Pick a template opens at once.
  useTemplates()
  const top = <TopBar title={t('dashboard.title')} aside={<TextAction to={accountPath}>{t('dashboard.account')}</TextAction>} />

  return (
    <LoadableScreen state={dashboard} top={top} what={t('dashboard.what')}>
      {({ today, strategies, waiting }) => (
        <Screen top={top} note={`${t('common.disclaimer')} ${t('common.pastResults')}`}>
          <VerifyNotice />
          <PageHeader title={t('dashboard.heading')} />
          <WaitingList items={waiting} />
          <div className={styles.cards}>
            {strategies.map((s) => (
              <StrategyCard key={s.id} strategy={s} today={today} waiting={waiting.find((w) => w.strategyId === s.id)} />
            ))}
            <Link to={pickPath} className={`ui-strong ${styles.add}`}>
              {t('dashboard.newStrategy')}
            </Link>
          </div>
          <p className={styles.feedback}>
            <TextAction to={`${accountPath}${SEND_FEEDBACK_HASH}`}>{t('feedback.send.title')}</TextAction>
          </p>
        </Screen>
      )}
    </LoadableScreen>
  )
}
