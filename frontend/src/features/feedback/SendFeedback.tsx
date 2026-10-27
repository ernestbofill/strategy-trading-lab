import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useLocation } from 'react-router-dom'
import { api, UnauthorizedError, type FeedbackKind } from '../../api'
import { FormMessage, TextAreaField, type FormMessageState } from '../../components/Field'
import { useT } from '../../i18n/I18n'
import { useCurrentAccount } from '../account/AccountContext'
import styles from '../account/Account.module.css'
import { feedbackErrorText } from './feedback'
import { FEEDBACK_SUBMITTED_EVENT } from './FeedbackCapture'
import { KindPicker } from './KindPicker'

/** The hash that opens the account page at this form ("Send feedback" on the dashboard). */
export const SEND_FEEDBACK_HASH = '#feedback'

/** "Send feedback" on the account page (ROADMAP item 26): a comment not tied to any text on a screen. */
export function SendFeedback() {
  const t = useT()
  const { emailVerified } = useCurrentAccount()
  const { hash } = useLocation()
  const [kind, setKind] = useState<FeedbackKind>('idea')
  const [text, setText] = useState('')
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)
  const ref = useRef<HTMLFormElement | null>(null)

  useEffect(() => {
    if (hash === SEND_FEEDBACK_HASH) ref.current?.scrollIntoView({ block: 'start' })
  }, [hash])

  if (!emailVerified) return <p className={styles.line}>{t('feedback.send.confirmFirst')}</p>

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const trimmed = text.trim()
    if (busy || !trimmed) return
    setBusy(true)
    setMessage(null)
    try {
      await api.submitFeedback({ kind, text: trimmed })
      window.dispatchEvent(new CustomEvent(FEEDBACK_SUBMITTED_EVENT))
      setText('')
      setMessage({ tone: 'done', text: t('feedback.send.sent') })
    } catch (err) {
      if (!(err instanceof UnauthorizedError)) setMessage({ tone: 'error', text: feedbackErrorText(err, t) })
    }
    setBusy(false)
  }

  return (
    <form ref={ref} className={styles.form} onSubmit={(e) => void submit(e)} noValidate>
      <p className={styles.line}>{t('feedback.send.intro')}</p>
      <KindPicker value={kind} onChange={setKind} />
      <TextAreaField label={t('feedback.yourComment')} hideLabel value={text} onChange={setText} rows={4} maxLength={4000} placeholder={t('feedback.send.placeholder')} />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || !text.trim()}>
        {busy ? t('feedback.send.sending') : t('feedback.send.action')}
      </button>
    </form>
  )
}
