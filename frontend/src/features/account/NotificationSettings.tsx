import { useId, useState } from 'react'
import { api, type Account } from '../../api'
import { Checkbox, FormMessage, type FormMessageState } from '../../components/Field'
import { useT } from '../../i18n/I18n'
import styles from './Account.module.css'
import { errorText } from './copy'
import { DevicePush } from './DevicePush'
import { usePushOffered } from './usePushOffered'

/**
 * The account page's Notifications (spec: Notifications): the only place they are set. Two kinds, each with its channels:
 * signals and reminders (app, email) and the summary (email). The App box and this device's line only where the app offers push.
 */
export function NotificationSettings({ account, onSaved }: { account: Account; onSaved: () => Promise<unknown> }) {
  const t = useT()
  const pushOffered = usePushOffered()
  return (
    <div className={styles.form}>
      <Kind title={t('account.notify.alerts')} hint={t('account.notify.alertsHint')}>
        {pushOffered && <Saved current={account.alertPush} label={t('account.notify.app')} save={api.setAlertPush} onSaved={onSaved} />}
        <Saved current={account.alertEmails} label={t('account.notify.email')} save={api.setAlertEmails} onSaved={onSaved} />
      </Kind>
      <Kind title={t('account.notify.summary')} hint={t('account.notify.summaryHint')}>
        <Saved current={account.overviewEmail} label={t('account.notify.email')} save={api.setOverviewEmail} onSaved={onSaved} />
      </Kind>
      {pushOffered && account.alertPush && (
        <div className={styles.kind}>
          <h3 className={`ui-strong ${styles.subTitle}`}>{t('account.notify.device')}</h3>
          <DevicePush />
        </div>
      )}
    </div>
  )
}

function Kind({ title, hint, children }: { title: string; hint: string; children: React.ReactNode }) {
  const id = useId()
  return (
    <div className={styles.kind} role="group" aria-labelledby={id}>
      <div>
        <h3 id={id} className={`ui-strong ${styles.subTitle}`}>{title}</h3>
        <p className={styles.line}>{hint}</p>
      </div>
      <div className={styles.channels}>{children}</div>
    </div>
  )
}

/** A checkbox that saves on change, with an error under it when the save fails. */
function Saved({ current, label, save, onSaved }: {
  current: boolean
  label: string
  save: (enabled: boolean) => Promise<void>
  onSaved: () => Promise<unknown>
}) {
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)

  const change = async (enabled: boolean) => {
    if (busy) return
    setBusy(true)
    setMessage(null)
    try {
      await save(enabled)
      await onSaved()
    } catch (err) {
      setMessage({ tone: 'error', text: errorText(err) })
    }
    setBusy(false)
  }

  return (
    <div>
      <Checkbox checked={current} onChange={(enabled) => void change(enabled)}>
        {label}
      </Checkbox>
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </div>
  )
}
