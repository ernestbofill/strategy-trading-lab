import { useEffect, useState } from 'react'
import { UnauthorizedError } from '../../api'
import { FormMessage } from '../../components/Field'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { deviceSubscription, pushKey, pushSupport, subscribeDevice, unsubscribeDevice } from '../../lib/push'
import styles from './Account.module.css'

/** `none`: the server sends no push (no keys), so there is nothing to turn on. */
type DeviceState = 'loading' | 'none' | 'on' | 'off'

/** The account page's "Notifications" section: push on this device, on or off. */
export function DevicePush() {
  const t = useT()
  const support = pushSupport()
  const [state, setState] = useState<DeviceState>('loading')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ tone: 'error' | 'done'; text: string } | null>(null)

  useEffect(() => {
    let live = true
    if (support !== 'ready') return
    // A subscription the browser has is saved again (it is idempotent): the server may have dropped it, given the device to
    // another account, or changed its key, and the page must not say On while nothing would arrive.
    Promise.all([pushKey(), deviceSubscription()])
      .then(async ([key, subscription]) => (key === null ? 'none' : subscription && (await subscribeDevice()) ? 'on' : 'off'))
      .then(
        (next) => live && setState(next),
        () => live && setState('off'),
      )
    return () => {
      live = false
    }
  }, [support])

  if (support === 'unsupported' || state === 'none') return <p className="caption">{t('account.push.unsupported')}</p>
  if (support === 'homeScreen') return <p className="caption">{t('account.push.homeScreen')}</p>
  if (support === 'blocked') return <p className="caption">{t('account.push.blocked')}</p>
  if (state === 'loading') return null

  const change = async () => {
    setBusy(true)
    setMessage(null)
    try {
      if (state === 'on') {
        await unsubscribeDevice()
        setState('off')
      } else if (await subscribeDevice()) {
        setState('on')
      } else {
        setMessage({ tone: 'error', text: t(pushSupport() === 'blocked' ? 'account.push.blocked' : 'account.push.refused') })
      }
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      setMessage({ tone: 'error', text: t('account.push.failed') })
    }
    setBusy(false)
  }

  return (
    <>
      <p className={styles.line}>{t(state === 'on' ? 'account.push.on' : 'account.push.off')}</p>
      <button type="button" className={`ui-strong ${styles.button}`} disabled={busy} onClick={() => void change()}>
        {t(state === 'on' ? 'account.push.turnOff' : 'account.push.turnOn')}
      </button>
      <p className="caption">{t('account.push.hint')}</p>
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </>
  )
}

/**
 * Right after a strategy is started with push on (Ernest, 2026-11-01): asks to allow notifications on this device, from a tap,
 * since Safari only asks then. Already allowed: it subscribes quietly and shows nothing.
 */
export function PushPrompt({ onDone }: { onDone: () => void }) {
  const t = useT()
  const support = pushSupport()
  const [busy, setBusy] = useState(false)
  const [result, setResult] = useState<'on' | 'refused' | 'failed' | null>(null)
  const granted = support === 'ready' && Notification.permission === 'granted'

  useEffect(() => {
    if (!granted) return
    subscribeDevice().then(onDone, onDone)
  }, [granted, onDone])

  if (granted || support === 'unsupported') return null

  const allow = async () => {
    setBusy(true)
    try {
      setResult((await subscribeDevice()) ? 'on' : 'refused')
    } catch {
      setResult('failed')
    }
    setBusy(false)
  }

  return (
    <div className={styles.pushPrompt} role="region" aria-label={t('account.push.promptTitle')}>
      <p className="ui-strong">{t('account.push.promptTitle')}</p>
      {result === 'on' ? (
        <FormMessage tone="done">{t('account.push.promptOn')}</FormMessage>
      ) : result ? (
        <FormMessage tone="error">{t(result === 'refused' ? 'account.push.promptRefused' : 'account.push.failed')}</FormMessage>
      ) : support === 'ready' ? (
        <>
          <p>{t('account.push.promptText')}</p>
          <button type="button" className={`ui-strong ${styles.button}`} disabled={busy} onClick={() => void allow()}>
            {t('account.push.promptAllow')}
          </button>
        </>
      ) : (
        <p>{t(support === 'homeScreen' ? 'account.push.homeScreen' : 'account.push.blocked')}</p>
      )}
      <TextAction onClick={onDone}>{t(result ? 'account.push.promptClose' : 'account.push.promptLater')}</TextAction>
    </div>
  )
}
