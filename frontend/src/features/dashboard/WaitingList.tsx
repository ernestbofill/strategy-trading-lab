import { Link } from 'react-router-dom'
import type { WaitingItem } from '../../api'
import { Tag } from '../../components/Tag'
import { aiRoundPath, signalPath } from './paths'
import styles from './WaitingList.module.css'

function target(item: WaitingItem) {
  return item.kind === 'aiRound' ? aiRoundPath(item.strategyId) : signalPath(item.strategyId, item.id)
}

function Message({ item }: { item: WaitingItem }) {
  const name = <b>{item.strategyName}</b>
  switch (item.kind) {
    case 'buySignal':
      return <>Your rules for {name} are met for {item.stockName}. Did you act on it?</>
    case 'sellSignal':
      return <>Your rules for {name} are met for a sale. Did you act on it?</>
    case 'aiRound':
      return <>Time for the AI round of {name}.</>
  }
}

/** "Waiting for you": one link per signal to answer or AI round due, with a count. Shown only when something waits. */
export function WaitingList({ items }: { items: WaitingItem[] }) {
  if (items.length === 0) return null
  return (
    <section className={styles.section}>
      <div className={styles.head}>
        <h2 className={`ui-strong ${styles.title}`}>Waiting for you</h2>
        <span className={styles.count}>
          {items.length}
          <span className="visually-hidden"> waiting</span>
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
