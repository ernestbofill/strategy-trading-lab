import type { ReactNode } from 'react'
import styles from './ChoiceCard.module.css'

/** Single-choice list of cards (template picker, mode picker). Flows into columns on wider screens. */
export function ChoiceList({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div role="radiogroup" aria-label={label} className={styles.list}>
      {children}
    </div>
  )
}

interface ChoiceCardProps {
  /** Radio group name, shared by every card in one ChoiceList. */
  name: string
  value: string
  checked: boolean
  onSelect: () => void
  title: ReactNode
  /** Small tag before the title, e.g. a SIM / LIVE badge. */
  badge?: ReactNode
  /** Line under the title, e.g. a template's Tagline. */
  subtitle?: ReactNode
  /** Card body, inside the selectable area. */
  children?: ReactNode
  /** Interactive content below the body (links), kept outside the radio's label. */
  footer?: ReactNode
}

/** A selectable card backed by a native radio input; the whole card is the tap target. */
export function ChoiceCard({ name, value, checked, onSelect, title, badge, subtitle, children, footer }: ChoiceCardProps) {
  return (
    <div className={styles.card} data-selected={checked || undefined}>
      <label className={styles.main}>
        <input
          type="radio"
          name={name}
          value={value}
          checked={checked}
          onChange={onSelect}
          className="visually-hidden"
        />
        <span className={styles.head}>
          {badge && <span className={styles.badge}>{badge}</span>}
          <span className={styles.headText}>
            <span className={styles.title}>{title}</span>
            {subtitle}
          </span>
          <span className={styles.radio} aria-hidden="true" />
        </span>
        {children && <span className={styles.body}>{children}</span>}
      </label>
      {footer && <div className={styles.footer}>{footer}</div>}
    </div>
  )
}
