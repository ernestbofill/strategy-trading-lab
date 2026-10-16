import type { ReactNode } from 'react'
import styles from './ItemRow.module.css'

interface ItemRowProps {
  /** `label` makes the whole row toggle the input passed as children. */
  as?: 'div' | 'label'
  leading?: ReactNode
  title: string
  detail?: string
  trailing?: ReactNode
  dimmed?: boolean
  children?: ReactNode
}

/** A stock or fund row: name, one line on what it does, optional control on either side. */
export function ItemRow({ as: Element = 'div', leading, title, detail, trailing, dimmed, children }: ItemRowProps) {
  return (
    <Element className={styles.row}>
      {children}
      {leading}
      <span className={styles.text}>
        <span className={styles.title} data-dimmed={dimmed || undefined}>
          {title}
        </span>
        {detail && <span className={styles.detail}>{detail}</span>}
      </span>
      {trailing}
    </Element>
  )
}
