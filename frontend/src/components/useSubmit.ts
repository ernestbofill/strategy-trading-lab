import { useState } from 'react'
import { InvalidFieldsError, InvalidValueError, UnauthorizedError } from '../api'
import type { FormMessageState } from './Field'

/**
 * A form's save: busy while it runs (a second submit does nothing), the server's refusals under their fields, else a line
 * saying why, in the server's words for a refused value or `fallback`. An ended session goes to Log-in: nothing is said.
 */
export function useSubmit(fallback: string) {
  const [busy, setBusy] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)

  /** Runs `action` unless `check` finds fields to fix first; resolves to whether it worked. */
  const run = async (action: () => Promise<unknown>, check?: () => Record<string, string>): Promise<boolean> => {
    if (busy) return false
    setErrors({})
    setMessage(null)
    const unclear = check?.() ?? {}
    if (Object.keys(unclear).length > 0) {
      setErrors(unclear)
      return false
    }
    setBusy(true)
    try {
      await action()
      return true
    } catch (e) {
      if (e instanceof UnauthorizedError) return false
      if (e instanceof InvalidFieldsError) setErrors(e.fields)
      else setMessage({ tone: 'error', text: e instanceof InvalidValueError ? e.message : fallback })
      return false
    } finally {
      setBusy(false)
    }
  }

  return { busy, errors, setErrors, message, setMessage, run }
}
