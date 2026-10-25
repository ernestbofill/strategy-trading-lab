import { Link } from 'react-router-dom'
import type { WaitingItem } from '../../api'
import { Tag } from '../../components/Tag'
import { useT } from '../../i18n/I18n'
import { aiRoundPath, signalPath } from './paths'
import styles from './WaitingList.module.css'

function target(item: WaitingItem) {
  return item.kind === 'aiRound' ? aiRoundPath(item.strategyId) : signalPath(item.strategyId, item.id)
}

function Message({ item }: { item: WaitingItem }) {
  const t = useT()
  switch (item.kind) {
    case 'buySignal':
      return <>{t('dashboard.waiting.buy', { strategy: item.strategyName, stock: item.stockName ?? '' })}</>
    case 'sellSignal':
      return <>{t('dashboard.waiting.sell', { strategy: item.strategyName })}</>
    case 'aiRound':
      return <>{t('dashboard.waiting.aiRound', { strategy: item.strategyName })}</>
  }
}

/** "Waiting for you": one link per signal to answer or AI round due, with a count. Shown only when something waits. */
export function WaitingList({ items }: { items: WaitingItem[] }) {
  const t = useT()
  if (items.length === 0) return null
  return (
    <section className={styles.section}>
      <div className={styles.head}>
        <h2 className={`ui-strong ${styles.title}`}>{t('dashboard.waitingTitle')}</h2>
        <span className={styles.count}>
          {items.length}
          <span className="visually-hidden">{t('dashboard.waitingHidden')}</span>
        </span>
      </div>
      <ul className={styles.list}>
        {items.map((item) => (
          <li key={item.id}>
            <Link to={target(item)} className={styles.row}>
              <Tag tone={item.kind === 'aiRound' ? 'ai' : 'live'}>{item.kind === 'aiRound' ? 'AI' : 'LIVE'}</Tag>
              <span className={styles.text}>
                <Message item={item} />
              </span>
              <span className={styles.chevron} aria-hidden="true">
                ›
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  )
}
