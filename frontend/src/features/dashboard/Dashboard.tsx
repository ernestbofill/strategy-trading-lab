import { Link } from 'react-router-dom'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { DISCLAIMER, PAST_RESULTS_CAPTION } from '../../lib/copy'
import { pickPath } from '../new-strategy/paths'
import { accountPath } from './paths'
import styles from './Dashboard.module.css'
import { StrategyCard } from './StrategyCard'
import { useDashboard } from './useDashboard'
import { WaitingList } from './WaitingList'

/** Dashboard (wireframe `Home`). */
export function Dashboard() {
  const dashboard = useDashboard()
  const top = <TopBar title="STL // DASHBOARD" aside={<TextAction to={accountPath}>Account</TextAction>} />

  return (
    <LoadableScreen state={dashboard} top={top} what="your strategies">
      {({ today, strategies, waiting }) => (
        <Screen top={top} note={`${DISCLAIMER} ${PAST_RESULTS_CAPTION}`}>
          <PageHeader title="Your strategies" />
          <WaitingList items={waiting} />
          <div className={styles.cards}>
            {strategies.map((s) => (
              <StrategyCard key={s.id} strategy={s} today={today} waiting={waiting.find((w) => w.strategyId === s.id)} />
            ))}
            <Link to={pickPath} className={`ui-strong ${styles.add}`}>
              + New strategy
            </Link>
          </div>
        </Screen>
      )}
    </LoadableScreen>
  )
}
