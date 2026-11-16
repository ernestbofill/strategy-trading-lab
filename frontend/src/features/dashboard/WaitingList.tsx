import type { WaitingItem } from '../../api'
import { LinkList, LinkSection } from '../../components/LinkList'
import { ModeTag } from '../../components/ModeTag'
import { Tag } from '../../components/Tag'
import { useT } from '../../i18n/I18n'
import { aiRoundPath, signalPath } from './paths'

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
    <LinkSection title={t('dashboard.waitingTitle')} count={items.length} countLabel={t('dashboard.waitingHidden')}>
      <LinkList
        items={items.map((item) => ({
          key: item.id,
          to: item.kind === 'aiRound' ? aiRoundPath(item.strategyId) : signalPath(item.strategyId, item.id),
          tag: item.kind === 'aiRound' ? <Tag tone="ai">AI</Tag> : <ModeTag live />,
          children: <Message item={item} />,
        }))}
      />
    </LinkSection>
  )
}
