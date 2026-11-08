import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { api, InvalidFieldsError, UnauthorizedError } from '../../api'
import { Checkbox, FormMessage, PasswordField, TextField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { AdminLink } from '../admin/AdminTopBar'
import { useT } from '../../i18n/I18n'
import styles from './Account.module.css'
import { useAccount, useCurrentAccount } from './AccountContext'
import { errorText } from './copy'
import { APP_HASH, NOTIFICATIONS_HASH, logInPath, settingsPath } from './paths'
import { homePath } from '../new-strategy/paths'
import { ResendLink } from './ResendLink'
import { unsubscribeDevice } from '../../lib/push'
import { AiSharingDetails } from '../ai-round/AiSharingDetails'
import { SEND_FEEDBACK_HASH } from '../feedback/SendFeedback'

/** Account (spec: Account): who the user is and their data. Reached from Settings' first row. */
export function AccountPage() {
  const t = useT()
  const account = useCurrentAccount()
  const { clear, refresh } = useAccount()
  const navigate = useNavigate()
  const [loggingOut, setLoggingOut] = useState(false)
  const [logOutError, setLogOutError] = useState<string | null>(null)
  const { hash } = useLocation()

  // Notifications, feedback and app install moved to Settings: an old link to them here goes on there.
  useEffect(() => {
    if (hash === NOTIFICATIONS_HASH || hash === SEND_FEEDBACK_HASH || hash === APP_HASH) navigate(`${settingsPath}${hash}`, { replace: true })
  }, [hash, navigate])

  const logOut = async () => {
    setLoggingOut(true)
    setLogOutError(null)
    try {
      // The next person on this device mustn't get this account's alerts: the device stops first (best effort).
      await unsubscribeDevice().catch(() => {})
      await api.logOut()
    } catch (err) {
      // Only a session that is already gone counts as logged out: otherwise the cookie still works, so say so.
      if (!(err instanceof UnauthorizedError)) {
        setLogOutError(errorText(err))
        setLoggingOut(false)
        return
      }
    }
    navigate(logInPath, { replace: true })
    clear()
  }

  return (
    <Screen top={<TopBar up={settingsPath} upLabel="common.settings" aside={account.role === 'owner' ? <AdminLink /> : null} />}>
      <PageHeader title={t('account.page.title')} />

      <Section title={t('account.page.email')}>
        <p className={styles.line}>
          <span className={styles.email}>{account.email}</span>
        </p>
        {!account.emailVerified && (
          <>
            <p className={styles.line}>{t('account.page.notVerified')}</p>
            <ResendLink />
          </>
        )}
      </Section>

      <Section title={t('account.page.name')}>
        <NameForm firstName={account.firstName ?? ''} lastName={account.lastName ?? ''} onSaved={refresh} />
      </Section>

      {account.role === 'owner' && (
        <Section title={t('account.page.admin')}>
          <p className={styles.line}>{t('account.page.adminLine')}</p>
          <Link to="/admin" className={`ui-strong ${styles.button} ${styles.linkButton}`}>
            {t('account.page.adminLink')}
          </Link>
        </Section>
      )}

      <Section title={t('account.page.dataPrivacy')}>
        <AiSharing current={account.aiSharing} onSaved={refresh} />
      </Section>

      <Section title={t('account.page.changePassword')}>
        <ChangePassword email={account.email} />
      </Section>

      <button type="button" className={`ui-strong ${styles.button}`} disabled={loggingOut} onClick={() => void logOut()}>
        {loggingOut ? t('account.page.loggingOut') : t('account.page.logOut')}
      </button>
      {logOutError && <FormMessage tone="error">{logOutError}</FormMessage>}

      <Section title={t('account.delete.title')}>
        {account.role === 'owner' ? <p className={styles.line}>{t('account.delete.owner')}</p> : <DeleteAccount email={account.email} />}
      </Section>
    </Screen>
  )
}

function NameForm({ firstName, lastName, onSaved }: { firstName: string; lastName: string; onSaved: () => Promise<unknown> }) {
  const t = useT()
  const [first, setFirst] = useState(firstName)
  const [last, setLast] = useState(lastName)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setErrors({})
    setMessage(null)
    try {
      await api.setName(first, last)
      if ((await onSaved()) === undefined) throw new Error('Could not reload the account.')
      setMessage({ tone: 'done', text: t('account.page.nameSaved') })
    } catch (err) {
      if (err instanceof InvalidFieldsError) setErrors(err.fields)
      else setMessage({ tone: 'error', text: errorText(err) })
    }
    setBusy(false)
  }

  return (
    <form className={styles.form} onSubmit={submit} noValidate>
      <div className={styles.names}>
        <TextField label={t('account.field.firstName')} autoComplete="given-name" autoCapitalize="words" value={first} onChange={setFirst} error={errors.firstName} />
        <TextField label={t('account.field.lastName')} autoComplete="family-name" autoCapitalize="words" value={last} onChange={setLast} error={errors.lastName} />
      </div>
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || (first === firstName && last === lastName)}>
        {busy ? t('account.saving') : t('account.page.nameSave')}
      </button>
    </form>
  )
}

function ChangePassword({ email }: { email: string }) {
  const t = useT()
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setErrors({})
    setMessage(null)
    try {
      await api.changePassword(current, next)
      setCurrent('')
      setNext('')
      setMessage({ tone: 'done', text: t('account.page.passwordChanged') })
    } catch (err) {
      if (err instanceof InvalidFieldsError) setErrors(err.fields)
      else setMessage({ tone: 'error', text: errorText(err) })
    }
    setBusy(false)
  }

  return (
    <form className={styles.form} onSubmit={submit} noValidate>
      {/* For password managers: which account the new password belongs to. */}
      <input type="email" name="username" autoComplete="username" value={email} readOnly hidden />
      <PasswordField
        label={t('account.page.currentPassword')}
        autoComplete="current-password"
        value={current}
        onChange={setCurrent}
        error={errors.currentPassword}
      />
      <PasswordField
        label={t('account.field.newPassword')}
        autoComplete="new-password"
        value={next}
        onChange={setNext}
        hint={t('account.passwordHint')}
        error={errors.newPassword}
      />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || !current || !next}>
        {busy ? t('account.saving') : t('account.page.changePassword')}
      </button>
    </form>
  )
}

/** Sharing AI rounds anonymously (spec: AI round, AI data sharing): off by default, asked at the first round. */
function AiSharing({ current, onSaved }: { current: boolean; onSaved: () => Promise<unknown> }) {
  const t = useT()
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)

  const change = async (enabled: boolean) => {
    if (busy) return
    setBusy(true)
    setMessage(null)
    try {
      await api.setAiSharing(enabled)
      await onSaved()
    } catch (err) {
      setMessage({ tone: 'error', text: errorText(err) })
    }
    setBusy(false)
  }

  return (
    <div className={styles.form}>
      <Checkbox checked={current} onChange={(enabled) => void change(enabled)}>
        {t('account.aiSharing')}
      </Checkbox>
      <AiSharingDetails />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </div>
  )
}

/** Delete my account (spec: Account): asks for the password, then deletes everything; the API ends every session and sends one last email. */
function DeleteAccount({ email }: { email: string }) {
  const t = useT()
  const [asking, setAsking] = useState(false)
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setError(null)
    setMessage(null)
    try {
      // This device stops its alerts first (best effort): its subscription goes with the account anyway.
      await unsubscribeDevice().catch(() => {})
      await api.deleteAccount(password)
      // A full load as a guest: no cached load of the deleted account survives, and no screen that needs it renders on the way.
      window.location.replace(homePath)
      return
    } catch (err) {
      if (err instanceof InvalidFieldsError && err.fields.password) setError(err.fields.password)
      else setMessage(errorText(err))
    }
    setBusy(false)
  }

  if (!asking)
    return (
      <div className={styles.form}>
        <p className={styles.line}>{t('account.delete.line')}</p>
        <button type="button" className={`ui-strong ${styles.button} ${styles.danger}`} onClick={() => setAsking(true)}>
          {t('account.delete.title')}
        </button>
      </div>
    )

  return (
    <form className={styles.form} onSubmit={submit} noValidate>
      <input type="email" name="username" autoComplete="username" value={email} readOnly hidden />
      <PasswordField label={t('account.delete.password')} autoComplete="current-password" value={password} onChange={setPassword} error={error} />
      {message && <FormMessage tone="error">{message}</FormMessage>}
      <div className={styles.links}>
        <button type="submit" className={`ui-strong ${styles.button} ${styles.danger}`} disabled={busy || !password}>
          {busy ? t('account.delete.deleting') : t('account.delete.confirm')}
        </button>
        <button type="button" className={`ui-strong ${styles.button}`} disabled={busy} onClick={() => { setAsking(false); setPassword(''); setError(null) }}>
          {t('account.delete.cancel')}
        </button>
      </div>
    </form>
  )
}
