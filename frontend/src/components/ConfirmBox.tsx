import { useState, type ReactNode } from 'react'
import { UnauthorizedError } from '../api'
import { Button, ButtonRow } from './Button'
import styles from './ConfirmBox.module.css'

interface ConfirmBoxProps {
  /** What it asks: "Delete this trade? …". */
  ask: ReactNode
  confirmLabel: string
  busyLabel: string
  keepLabel: string
  /** Said when `onConfirm` fails. */
  failedText: string
  /** Does it; a rejection says `failedText` (an ended session goes to Log-in, so it says nothing). */
  onConfirm: () => Promise<void>
  onKeep: () => void
  /** Placement from the caller (the strategy page's tile grid makes it the whole row). */
  className?: string
}

/**
 * The confirm step of something that can't be undone (delete a trade or a strategy, archive it): the question, the danger
 * button and the safe one, which gets the focus so the keyboard lands in the step.
 */
export function ConfirmBox({ ask, confirmLabel, busyLabel, keepLabel, failedText, onConfirm, onKeep, className }: ConfirmBoxProps) {
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)

  const confirm = async () => {
    setBusy(true)
    setFailed(false)
    try {
      await onConfirm()
      // Done: the screen changes (the item goes, the page reloads); its buttons stay off meanwhile.
    } catch (e) {
      if (e instanceof UnauthorizedError) return
      setFailed(true)
      setBusy(false)
    }
  }

  return (
    <div className={className ? `${styles.box} ${className}` : styles.box}>
      <p>{ask}</p>
      {failed && <p role="alert">{failedText}</p>}
      <ButtonRow>
        <Button variant="danger" disabled={busy} onClick={() => void confirm()}>
          {busy ? busyLabel : confirmLabel}
        </Button>
        <Button disabled={busy} onClick={onKeep} autoFocus>
          {keepLabel}
        </Button>
      </ButtonRow>
    </div>
  )
}
