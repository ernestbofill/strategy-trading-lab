import { useEffect, useId, useRef, useState } from 'react'
import styles from './InfoTip.module.css'

/**
 * A small "i" after a label that opens a short explanation, kept hidden until asked for. The text spans the nearest
 * positioned ancestor, so give the row it explains `position: relative`; Escape or a click elsewhere closes it.
 */
export function InfoTip({ label, text }: { label: string; text: string }) {
  const [open, setOpen] = useState(false)
  const id = useId()
  const ref = useRef<HTMLSpanElement>(null)

  useEffect(() => {
    if (!open) return
    const close = (e: Event) => {
      if (e instanceof KeyboardEvent ? e.key === 'Escape' : !ref.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('keydown', close)
    document.addEventListener('pointerdown', close)
    return () => {
      document.removeEventListener('keydown', close)
      document.removeEventListener('pointerdown', close)
    }
  }, [open])

  return (
    <span ref={ref} className={styles.tip}>
      <button
        type="button"
        className={styles.button}
        aria-label={label}
        aria-expanded={open}
        aria-controls={open ? id : undefined}
        onClick={() => setOpen((o) => !o)}
      >
        i
      </button>
      {open && (
        <span id={id} role="note" className={styles.text}>
          {text}
        </span>
      )}
    </span>
  )
}
