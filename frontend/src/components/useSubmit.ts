import { useRef, useState } from 'react'
import { InvalidFieldsError, InvalidValueError, UnauthorizedError } from '../api'
import type { FormMessageState } from './Field'

/**
 * A form's save: busy while it runs (a second submit does nothing), the server's refusals under their fields, else a line
 * saying why, in the server's words for a refused value or `fallback` (a function picks the line by the error). An ended session goes to Log-in: nothing is said.
 * After a save that worked it stays busy, so a second tap can't send it again while the screen moves on or the form closes;
 * a form that stays open after a save calls `reset()` to take the next one.
 */
export function useSubmit(fallback: string | ((error: unknown) => string)) {
  const [busy, setBusy] = useState(false)
  // The guard itself: two submits before React renders again (Enter and a tap) both see busy as false.
  const running = useRef(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)

  /**
   * Runs `action` unless `check` finds fields to fix first; resolves to whether it worked. After it worked the form stays
   * busy: the screen moves on, and a second tap must not send it again.
   */
  const run = async (action: () => Promise<unknown>, check?: () => Record<string, string>): Promise<boolean> => {
    if (running.current) return false
    setErrors({})
    setMessage(null)
    const unclear = check?.() ?? {}
    if (Object.keys(unclear).length > 0) {
      setErrors(unclear)
      return false
    }
    running.current = true
    setBusy(true)
    try {
      await action()
      return true
    } catch (e) {
      running.current = false
      setBusy(false)
      if (e instanceof UnauthorizedError) return false
      if (e instanceof InvalidFieldsError) setErrors(e.fields)
      else setMessage({ tone: 'error', text: e instanceof InvalidValueError ? e.message : typeof fallback === 'function' ? fallback(e) : fallback })
      return false
    }
  }

  /** Ready for the next save, after one that worked on a form that stays open. */
  const reset = () => {
    running.current = false
    setBusy(false)
  }

  return { busy, errors, setErrors, message, setMessage, run, reset }
}
