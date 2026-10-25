import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, InvalidFieldsError, UnauthorizedError, type Currency, type Locale } from '../../api'
import { Checkbox, FormMessage, PasswordField, SelectField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { useI18n, useT } from '../../i18n/I18n'
import { LOCALES } from '../../i18n/locale'
import { dashboardPath } from '../new-strategy/paths'
import styles from './Account.module.css'
import { useAccount, useCurrentAccount } from './AccountContext'
import { currencyOptionsWith } from './currencies'
import { errorText } from './copy'
import { logInPath } from './paths'
import { ResendLink } from './ResendLink'
import { InstallApp } from './InstallApp'

/** Account, first version (spec: Account): the email, change password, log out. */
export function AccountPage() {
  const t = useT()
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
    <Screen top={<TopBar back={dashboardPath} step={t('account.page.step')} />}>
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

      <Section title={t('account.page.app')}>
        <InstallApp />
      </Section>

      <Section title={t('account.page.language')}>
        <LanguageForm current={account.locale} onSaved={refresh} />
      </Section>

      <Section title={t('account.page.currency')}>
        <CurrencyForm current={account.currency} onSaved={refresh} />
      </Section>

      <Section title={t('account.page.emails')}>
        <OverviewEmail current={account.overviewEmail} onSaved={refresh} />
      </Section>

      <Section title={t('account.page.changePassword')}>
        <ChangePassword email={account.email} />
      </Section>

      <button type="button" className={`ui-strong ${styles.button}`} disabled={loggingOut} onClick={() => void logOut()}>
        {loggingOut ? t('account.page.loggingOut') : t('account.page.logOut')}
      </button>
      {logOutError && <FormMessage tone="error">{logOutError}</FormMessage>}
    </Screen>
  )
}

function LanguageForm({ current, onSaved }: { current: Locale; onSaved: () => Promise<unknown> }) {
  const t = useT()
  const { setLocale: setAppLocale } = useI18n()
  const [locale, setLocale] = useState(current)
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setMessage(null)
    try {
      await api.setLocale(locale)
      setAppLocale(locale)
      if ((await onSaved()) === undefined) throw new Error('Could not reload the account.')
      setMessage({ tone: 'done', text: t('account.page.languageSaved') })
    } catch (err) {
      setMessage({ tone: 'error', text: (err instanceof InvalidFieldsError && err.fields.locale) || errorText(err) })
    }
    setBusy(false)
  }

  return (
    <form className={styles.form} onSubmit={submit} noValidate>
      <SelectField label={t('account.page.language')} hideLabel value={locale} onChange={(v) => setLocale(v as Locale)} options={LOCALES} />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || locale === current}>
        {busy ? t('account.saving') : t('account.page.languageSave')}
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

function CurrencyForm({ current, onSaved }: { current: Currency; onSaved: () => Promise<unknown> }) {
  const t = useT()
  const [currency, setCurrency] = useState(current)
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)
  const options = currencyOptionsWith(t)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setMessage(null)
    try {
      await api.setCurrency(currency)
      if ((await onSaved()) === undefined) throw new Error('Could not reload the account.')
      setMessage({ tone: 'done', text: t('account.page.currencySaved') })
    } catch (err) {
      setMessage({ tone: 'error', text: (err instanceof InvalidFieldsError && err.fields.currency) || errorText(err) })
    }
    setBusy(false)
  }

  return (
    <form className={styles.form} onSubmit={submit} noValidate>
      <SelectField label={t('account.page.currency')} hideLabel value={currency} onChange={(v) => setCurrency(v as Currency)} options={options} />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || currency === current}>
        {busy ? t('account.saving') : t('account.page.currencySave')}
      </button>
    </form>
  )
}

function OverviewEmail({ current, onSaved }: { current: boolean; onSaved: () => Promise<unknown> }) {
  const t = useT()
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
        {t('account.overviewEmail')}
      </Checkbox>
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </div>
  )
}
