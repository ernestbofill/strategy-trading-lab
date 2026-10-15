import type { ReactNode } from 'react'
import styles from './CheckList.module.css'
import { ItemRow } from './ItemRow'

interface CheckItem {
  id: string
  title: string
  detail?: string
}

interface CheckListProps {
  label: string
  items: CheckItem[]
  isChecked: (id: string) => boolean
  onToggle: (id: string) => void
  /** Extra row at the end, e.g. "+ Add a stock". */
  footer?: ReactNode
}

/** Multi-choice list of rows; tapping a row ticks or unticks it, unticked rows are dimmed. */
export function CheckList({ label, items, isChecked, onToggle, footer }: CheckListProps) {
  return (
    <div className={styles.list} role="group" aria-label={label}>
      {items.map((item) => {
        const checked = isChecked(item.id)
        return (
          <ItemRow
            key={item.id}
            as="label"
            title={item.title}
            detail={item.detail}
            dimmed={!checked}
            leading={
              <span className={styles.box} data-checked={checked || undefined} aria-hidden="true">
                {checked && (
                  <svg width="12" height="12" viewBox="0 0 12 12">
                    <path d="M2 6.5l2.6 2.5L10 3.5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="square" />
                  </svg>
                )}
              </span>
            }
          >
            <input type="checkbox" checked={checked} onChange={() => onToggle(item.id)} className="visually-hidden" />
          </ItemRow>
        )
      })}
      {footer && <div className={styles.footer}>{footer}</div>}
    </div>
  )
}
