import type { ComponentPropsWithoutRef, ElementType } from 'react'
import styles from './Card.module.css'

type CardProps<T extends ElementType> = {
  /** The element or component to render: a div by default, or a button, a Link… */
  as?: T
  /** Hover lightens it: for a card that is a link, a button or a choice. */
  interactive?: boolean
  /** Picked or open: the green edge and glow. */
  selected?: boolean
  /** Can't be used for now: no hover. */
  disabled?: boolean
} & Omit<ComponentPropsWithoutRef<T>, 'as'>

/**
 * The app's card: its surface, edge, padding and states, the same everywhere (template and mode pickers, dashboard,
 * welcome). The caller adds its own layout with `className`.
 */
export function Card<T extends ElementType = 'div'>({ as, interactive, selected, disabled, className, ...rest }: CardProps<T>) {
  const Element: ElementType = as ?? 'div'
  return (
    <Element
      className={className ? `${styles.card} ${className}` : styles.card}
      data-interactive={interactive || undefined}
      data-selected={selected || undefined}
      data-disabled={disabled || undefined}
      {...rest}
    />
  )
}
