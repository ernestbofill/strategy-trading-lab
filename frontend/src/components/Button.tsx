import type { ButtonHTMLAttributes, ReactNode } from 'react'
import styles from './Button.module.css'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger'
}

/** A form's own button (Save, Cancel, a confirm step's Delete). The screen's one action is `PrimaryAction`. */
export function Button({ variant = 'secondary', type = 'button', className, ...rest }: ButtonProps) {
  return <button type={type} className={className ? `${styles.button} ${className}` : styles.button} data-variant={variant} {...rest} />
}

/** Buttons side by side, wrapping on a narrow screen. */
export const ButtonRow = ({ children }: { children: ReactNode }) => <div className={styles.row}>{children}</div>
