import { useState } from 'react'
import { api } from '../../api'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { errorText } from './copy'

/** "Send the link again": the verification email, for the logged-in user. */
export function ResendLink({ label }: { label?: string }) {
  const t = useT()
  const displayLabel = label ?? t('account.resend.label')
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

  if (state === 'sent') return <p role="status">{t('account.resend.sent')}</p>
  return (
    <div>
      <TextAction disabled={state === 'sending'} onClick={() => void send()}>
        {displayLabel}
      </TextAction>
      {error && <span role="alert"> {error}</span>}
    </div>
  )
}
