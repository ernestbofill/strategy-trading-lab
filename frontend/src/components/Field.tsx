import { useId, useState, type ReactNode } from 'react'
import styles from './Field.module.css'

interface FieldShellProps {
  id: string
  label: string
  hint?: ReactNode
  error?: string | null
  children: ReactNode
  aside?: ReactNode
  /** Keep the label for screen readers only, where a section heading above already says it. */
  hideLabel?: boolean
}

/** Label, control, hint and the error under it ("Use at least 10 characters."). */
function FieldShell({ id, label, hint, error, aside, hideLabel, children }: FieldShellProps) {
  return (
    <div className={styles.field}>
      <label htmlFor={id} className={hideLabel ? 'visually-hidden' : 'ui-strong'}>
        {label}
      </label>
      <div className={styles.control}>
        {children}
        {aside}
      </div>
      {hint && !error && (
        <p id={`${id}-hint`} className="caption">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className={styles.error}>
          {error}
        </p>
      )}
    </div>
  )
}

const describedBy = (id: string, hint: unknown, error: unknown) => (error ? `${id}-error` : hint ? `${id}-hint` : undefined)

interface TextFieldProps {
  label: string
  value: string
  onChange: (value: string) => void
  /** 'decimal' is a text field with the number keypad: a price, an amount. */
  type?: 'email' | 'text' | 'date' | 'decimal'
  autoComplete?: string
  placeholder?: string
  hint?: ReactNode
  error?: string | null
  /** Next to the field: a toggle such as "Enter shares instead". */
  aside?: ReactNode
  /** The first and last day a date field allows ("2026-10-22"). */
  min?: string
  max?: string
}

export function TextField({ label, value, onChange, type = 'text', autoComplete, placeholder, hint, error, aside, min, max }: TextFieldProps) {
  const id = useId()
  return (
    <FieldShell id={id} label={label} hint={hint} error={error} aside={aside}>
      <input
        id={id}
        className={styles.input}
        type={type === 'decimal' ? 'text' : type}
        inputMode={type === 'email' ? 'email' : type === 'decimal' ? 'decimal' : undefined}
        placeholder={placeholder}
        min={min}
        max={max}
        autoComplete={autoComplete}
        autoCapitalize="off"
        spellCheck={false}
        value={value}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(id, hint, error)}
        onChange={(e) => onChange(e.target.value)}
      />
    </FieldShell>
  )
}

interface TextAreaFieldProps {
  label: string
  value: string
  onChange: (value: string) => void
  rows?: number
  maxLength?: number
  placeholder?: string
  hint?: ReactNode
  error?: string | null
  hideLabel?: boolean
}

/** Several lines of text: a pasted answer, a prompt's instructions. */
export function TextAreaField({ label, value, onChange, rows = 8, maxLength, placeholder, hint, error, hideLabel }: TextAreaFieldProps) {
  const id = useId()
  return (
    <FieldShell id={id} label={label} hint={hint} error={error} hideLabel={hideLabel}>
      <textarea
        id={id}
        className={`${styles.input} ${styles.textarea}`}
        rows={rows}
        maxLength={maxLength}
        placeholder={placeholder}
        autoCapitalize="off"
        spellCheck={false}
        value={value}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(id, hint, error)}
        onChange={(e) => onChange(e.target.value)}
      />
    </FieldShell>
  )
}

interface PasswordFieldProps {
  label: string
  value: string
  onChange: (value: string) => void
  /** "current-password" on log-in, "new-password" where one is chosen. */
  autoComplete: 'current-password' | 'new-password'
  hint?: ReactNode
  error?: string | null
}

/** A password with a show/hide toggle. */
export function PasswordField({ label, value, onChange, autoComplete, hint, error }: PasswordFieldProps) {
  const id = useId()
  const [shown, setShown] = useState(false)
  return (
    <FieldShell
      id={id}
      label={label}
      hint={hint}
      error={error}
      aside={
        <button
          type="button"
          className={`ui-strong ${styles.toggle}`}
          aria-pressed={shown}
          aria-controls={id}
          onClick={() => setShown(!shown)}
        >
          {shown ? 'Hide' : 'Show'}
          <span className="visually-hidden"> password</span>
        </button>
      }
    >
      <input
        id={id}
        className={styles.input}
        type={shown ? 'text' : 'password'}
        autoComplete={autoComplete}
        autoCapitalize="off"
        spellCheck={false}
        maxLength={128}
        value={value}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(id, hint, error)}
        onChange={(e) => onChange(e.target.value)}
      />
    </FieldShell>
  )
}

interface SelectFieldProps {
  label: string
  value: string
  onChange: (value: string) => void
  options: { value: string; label: string }[]
  error?: string | null
  hideLabel?: boolean
}

export function SelectField({ label, value, onChange, options, error, hideLabel }: SelectFieldProps) {
  const id = useId()
  return (
    <FieldShell id={id} label={label} error={error} hideLabel={hideLabel}>
      <div className={styles.selectWrap}>
        <select
          id={id}
          className={styles.select}
          value={value}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy(id, null, error)}
          onChange={(e) => onChange(e.target.value)}
        >
          {options.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        <span className={styles.selectArrow} aria-hidden="true">
          <svg width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M2 4.5l4 4 4-4" />
          </svg>
        </span>
      </div>
    </FieldShell>
  )
}

interface CheckboxProps {
  checked: boolean
  onChange: (checked: boolean) => void
  children: ReactNode
  error?: string | null
}

/** A native checkbox, visually hidden inside its label, with the box drawn like CheckList's. */
export function Checkbox({ checked, onChange, children, error }: CheckboxProps) {
  const id = useId()
  return (
    <div className={styles.checkField}>
      <label className={styles.check}>
        <input
          type="checkbox"
          className="visually-hidden"
          checked={checked}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${id}-error` : undefined}
          onChange={(e) => onChange(e.target.checked)}
        />
        <span className={styles.box} data-checked={checked || undefined} aria-hidden="true">
          {checked && (
            <svg width="12" height="12" viewBox="0 0 12 12">
              <path d="M2 6.5l2.6 2.5L10 3.5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="square" />
            </svg>
          )}
        </span>
        <span className={styles.checkText}>{children}</span>
      </label>
      {error && (
        <p id={`${id}-error`} role="alert" className={styles.error}>
          {error}
        </p>
      )}
    </div>
  )
}

/** A form's message that isn't about one field, as a screen keeps it in state. */
export type FormMessageState = { tone: 'error' | 'done'; text: string } | null

/** A form's message that isn't about one field: "That email and password don't match." */
export function FormMessage({ tone, children }: { tone: 'error' | 'done'; children: ReactNode }) {
  return (
    <p role={tone === 'error' ? 'alert' : 'status'} className={tone === 'error' ? styles.error : styles.done}>
      {children}
    </p>
  )
}
