import { useState } from 'react'
import { api } from '../../api'
import { TextAction } from '../../components/TextAction'
import { errorText } from './copy'

/** "Send the link again": the verification email, for the logged-in user. */
export function ResendLink({ label = 'Send the link again' }: { label?: string }) {
  const [state, setState] = useState<'idle' | 'sending' | 'sent'>('idle')
  const [error, setError] = useState<string | null>(null)

  const send = async () => {
    setState('sending')
    setError(null)
    try {
      await api.resendVerification()
      setState('sent')
    } catch (err) {
      setError(errorText(err))
      setState('idle')
    }
  }

  if (state === 'sent') return <p role="status">Sent. Check your email.</p>
  return (
    <div>
      <TextAction disabled={state === 'sending'} onClick={() => void send()}>
        {label}
      </TextAction>
      {error && <span role="alert"> {error}</span>}
    </div>
  )
}
