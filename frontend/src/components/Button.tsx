import type { ButtonHTMLAttributes, ReactNode } from 'react'
import styles from './Button.module.css'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** `outline`: a quieter action beside a form (Save on Account, the admin pages' tools); `outline-danger`: one that deletes. */
  variant?: 'primary' | 'secondary' | 'danger' | 'outline' | 'outline-danger'
}

/** A form's own button (Save, Cancel, a confirm step's Delete). The screen's one action is `PrimaryAction`. */
export function Button({ variant = 'secondary', type = 'button', className, ...rest }: ButtonProps) {
  return <button type={type} className={className ? `${styles.button} ${className}` : styles.button} data-variant={variant} {...rest} />
}

/** The same look on a link that leads somewhere (`<a>`, react-router's `Link`): spread it onto the element. */
// eslint-disable-next-line react-refresh/only-export-components
export const buttonLook = (variant: NonNullable<ButtonProps['variant']>) => ({ className: styles.button, 'data-variant': variant })

/** Buttons side by side, wrapping on a narrow screen. */
export const ButtonRow = ({ children, className }: { children: ReactNode; className?: string }) => (
  <div className={className ? `${styles.row} ${className}` : styles.row}>{children}</div>
)
