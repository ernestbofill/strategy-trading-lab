import type { KeyboardEvent, ReactNode } from 'react'
import styles from './Tabs.module.css'
import { tabPanel } from './tabPanel'

export interface TabItem {
  key: string
  label: ReactNode
}

interface TabsProps {
  /** What the tabs choose between, for screen readers ("Index"). */
  label: string
  items: TabItem[]
  selected: string
  onSelect: (key: string) => void
  /** Unique on the page (`useId()`): ties each tab to its panel, see `tabPanel`. */
  id: string
  /** One panel whose content follows the tab (How we tested's table), instead of a panel per tab: every tab controls it. */
  sharedPanel?: boolean
}

/**
 * A row of tabs (ARIA tablist): click one, or move with ←/→, Home and End. The tabs wrap onto more rows rather than scroll
 * sideways. Each panel takes `tabPanel(id, key, sharedPanel)` (tabPanel.ts).
 */
export function Tabs({ label, items, selected, onSelect, id, sharedPanel }: TabsProps) {
  const onKeyDown = (e: KeyboardEvent<HTMLButtonElement>, index: number) => {
    let next = -1
    if (e.key === 'ArrowRight') next = (index + 1) % items.length
    else if (e.key === 'ArrowLeft') next = (index - 1 + items.length) % items.length
    else if (e.key === 'Home') next = 0
    else if (e.key === 'End') next = items.length - 1
    if (next < 0) return
    e.preventDefault()
    onSelect(items[next].key)
    document.getElementById(`${id}-tab-${items[next].key}`)?.focus()
  }

  return (
    <div className={styles.tabs} role="tablist" aria-label={label}>
      {items.map((item, i) => {
        const isSelected = item.key === selected
        return (
          <button
            key={item.key}
            id={`${id}-tab-${item.key}`}
            type="button"
            role="tab"
            aria-selected={isSelected}
            aria-controls={tabPanel(id, item.key, sharedPanel).id}
            tabIndex={isSelected ? 0 : -1}
            className={styles.tab}
            onClick={() => onSelect(item.key)}
            onKeyDown={(e) => onKeyDown(e, i)}
          >
            {item.label}
          </button>
        )
      })}
    </div>
  )
}
