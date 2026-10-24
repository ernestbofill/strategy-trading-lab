import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, InvalidFieldsError, UnauthorizedError, type Currency } from '../../api'
import { Checkbox, FormMessage, PasswordField, SelectField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { dashboardPath } from '../new-strategy/paths'
import styles from './Account.module.css'
import { useAccount, useCurrentAccount } from './AccountContext'
import { currencyOptions } from './currencies'
import { errorText } from './copy'
import { logInPath } from './paths'
import { ResendLink } from './ResendLink'

/** Account, first version (spec: Account): the email, change password, log out. */
export function AccountPage() {
  const account = useCurrentAccount()
  const { clear, refresh } = useAccount()
  const navigate = useNavigate()
  const [loggingOut, setLoggingOut] = useState(false)
  const [logOutError, setLogOutError] = useState<string | null>(null)

  const logOut = async () => {
    setLoggingOut(true)
    setLogOutError(null)
    try {
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
    <Screen top={<TopBar back={dashboardPath} step="ACCOUNT" />}>
      <PageHeader title="Account" />

      <Section title="Email">
        <p className={styles.line}>
          <span className={styles.email}>{account.email}</span>
        </p>
        {!account.emailVerified && (
          <>
            <p className={styles.line}>Not verified yet.</p>
            <ResendLink />
          </>
        )}
      </Section>

      <Section title="Currency">
        <CurrencyForm current={account.currency} onSaved={refresh} />
      </Section>

      <Section title="Emails">
        <OverviewEmail current={account.overviewEmail} onSaved={refresh} />
      </Section>

      <Section title="Change password">
        <ChangePassword email={account.email} />
      </Section>

      <button type="button" className={`ui-strong ${styles.button}`} disabled={loggingOut} onClick={() => void logOut()}>
        {loggingOut ? 'Logging out…' : 'Log out'}
      </button>
      {logOutError && <FormMessage tone="error">{logOutError}</FormMessage>}
    </Screen>
  )
}

function ChangePassword({ email }: { email: string }) {
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
      setMessage({ tone: 'done', text: 'Password changed. Your other devices are logged out.' })
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
        label="Current password"
        autoComplete="current-password"
        value={current}
        onChange={setCurrent}
        error={errors.currentPassword}
      />
      <PasswordField
        label="New password"
        autoComplete="new-password"
        value={next}
        onChange={setNext}
        hint="At least 10 characters."
        error={errors.newPassword}
      />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || !current || !next}>
        {busy ? 'Saving…' : 'Change password'}
      </button>
    </form>
  )
}

function CurrencyForm({ current, onSaved }: { current: Currency; onSaved: () => Promise<unknown> }) {
  const [currency, setCurrency] = useState(current)
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setMessage(null)
    try {
      await api.setCurrency(currency)
      if ((await onSaved()) === undefined) throw new Error('Could not reload the account.')
      setMessage({ tone: 'done', text: 'Saved. New strategies use it; running ones keep theirs.' })
    } catch (err) {
      setMessage({ tone: 'error', text: (err instanceof InvalidFieldsError && err.fields.currency) || errorText(err) })
    }
    setBusy(false)
  }

  return (
    <form className={styles.form} onSubmit={submit} noValidate>
      <SelectField label="Currency" hideLabel value={currency} onChange={(v) => setCurrency(v as Currency)} options={currencyOptions} />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || currency === current}>
        {busy ? 'Saving…' : 'Save currency'}
      </button>
    </form>
  )
}

function OverviewEmail({ current, onSaved }: { current: boolean; onSaved: () => Promise<unknown> }) {
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)

  const change = async (enabled: boolean) => {
    if (busy) return
    setBusy(true)
    setMessage(null)
    try {
      await api.setOverviewEmail(enabled)
      await onSaved()
    } catch (err) {
      setMessage({ tone: 'error', text: errorText(err) })
    }
    setBusy(false)
  }

  return (
    <div className={styles.form}>
      <Checkbox checked={current} onChange={(enabled) => void change(enabled)}>
        Email me a daily overview, early in the morning.
      </Checkbox>
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </div>
  )
}
