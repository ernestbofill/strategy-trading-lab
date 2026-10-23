import { useState } from 'react'
import { api, TooManyTriesError } from '../../api'
import styles from './Account.module.css'
import { TOO_MANY_TRIES } from './copy'

/** "Send the link again": the verification email, for the logged-in user. */
export function ResendLink({ label = 'Send the link again' }: { label?: string }) {
  const [state, setState] = useState<'idle' | 'sending' | 'sent' | 'tooMany' | 'failed'>('idle')

  const send = async () => {
    setState('sending')
    try {
      await api.resendVerification()
      setState('sent')
    } catch (err) {
      setState(err instanceof TooManyTriesError ? 'tooMany' : 'failed')
    }
  }

  if (state === 'sent') return <span role="status">Sent. Check your email.</span>
  return (
    <>
      <button type="button" className={`ui-strong ${styles.textButton}`} disabled={state === 'sending'} onClick={() => void send()}>
        {label}
      </button>
      {state === 'tooMany' && <span role="alert"> {TOO_MANY_TRIES}</span>}
      {state === 'failed' && <span role="alert"> Couldn't send it. Try again.</span>}
    </>
  )
}
