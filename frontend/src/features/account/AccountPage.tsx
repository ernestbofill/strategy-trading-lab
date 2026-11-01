import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { api, InvalidFieldsError, UnauthorizedError, type Currency, type Locale } from '../../api'
import { Checkbox, FormMessage, PasswordField, SegmentedField, TextField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { Tag } from '../../components/Tag'
import { useI18n, useT } from '../../i18n/I18n'
import { LOCALES } from '../../i18n/locale'
import { invalidate, useLoadable } from '../../lib/useLoadable'
import { dashboardPath } from '../new-strategy/paths'
import styles from './Account.module.css'
import { useAccount, useCurrentAccount } from './AccountContext'
import { currencyOptionsWith } from './currencies'
import { errorText } from './copy'
import { EMAILS_HASH, logInPath } from './paths'
import { ResendLink } from './ResendLink'
import { InstallApp } from './InstallApp'
import { AiSharingDetails } from '../ai-round/AiSharingDetails'
import { SendFeedback } from '../feedback/SendFeedback'

/** Account, first version (spec: Account): the email, change password, log out. */
export function AccountPage() {
  const t = useT()
  const account = useCurrentAccount()
  const { clear, refresh } = useAccount()
  const navigate = useNavigate()
  const [loggingOut, setLoggingOut] = useState(false)
  const [logOutError, setLogOutError] = useState<string | null>(null)
  const { hash } = useLocation()

  // Confirm's "Edit" on Daily overview opens the page at its Emails section.
  useEffect(() => {
    if (hash === EMAILS_HASH) document.getElementById(EMAILS_HASH.slice(1))?.scrollIntoView({ block: 'start' })
  }, [hash])

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

      <Section title={t('account.page.name')}>
        <NameForm firstName={account.firstName ?? ''} lastName={account.lastName ?? ''} onSaved={refresh} />
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

      {account.role === 'owner' && (
        <Section title={t('account.page.admin')}>
          <p className={styles.line}>{t('account.page.adminLine')}</p>
          <Link to="/admin" className={`ui-strong ${styles.button} ${styles.linkButton}`}>
            {t('account.page.adminLink')}
          </Link>
        </Section>
      )}

      <Section id={EMAILS_HASH.slice(1)} title={t('account.page.emails')}>
        <EmailSwitch current={account.overviewEmail} label={t('account.overviewEmail')} save={api.setOverviewEmail} onSaved={refresh} />
        <EmailSwitch current={account.alertEmails} label={t('account.alertEmails')} save={api.setAlertEmails} onSaved={refresh} />
        {account.overviewEmail && <StockChangeEmailList />}
      </Section>

      <Section title={t('account.page.dataPrivacy')}>
        <AiSharing current={account.aiSharing} onSaved={refresh} />
      </Section>

      <Section title={t('feedback.send.title')}>
        <SendFeedback />
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
      <SegmentedField label={t('account.page.language')} hideLabel value={locale} onChange={(v) => setLocale(v as Locale)} options={LOCALES} />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || locale === current}>
        {busy ? t('account.saving') : t('account.page.languageSave')}
      </button>
    </form>
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
      <SegmentedField label={t('account.page.currency')} hideLabel value={currency} onChange={(v) => setCurrency(v as Currency)} options={options} />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || currency === current}>
        {busy ? t('account.saving') : t('account.page.currencySave')}
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

const stockChangeEmailsKey = 'account:stock-change-emails'

/** Item 29: per running strategy, whether the daily overview tells its stock changes. Hidden with no running strategy. */
function StockChangeEmailList() {
  const t = useT()
  const list = useLoadable(stockChangeEmailsKey, api.getStockChangeEmails)
  if (list.status !== 'ready' || list.data.length === 0) return null
  const reload = async () => invalidate(stockChangeEmailsKey)
  return (
    <div className={styles.form}>
      <div>
        <h3 className={`ui-strong ${styles.subTitle}`}>{t('account.stockChanges.title')}</h3>
        <p className={styles.line}>{t('account.stockChanges.lead')}</p>
      </div>
      {list.data.map((s) => (
        <EmailSwitch
          key={s.strategyId}
          current={s.enabled}
          label={<>{s.name} <Tag tone={s.mode === 'live' ? 'live' : 'sim'}>{s.mode === 'live' ? 'LIVE' : 'SIM'}</Tag></>}
          save={(enabled) => api.setStockChangeEmails(s.strategyId, enabled)}
          onSaved={reload}
        />
      ))}
    </div>
  )
}

function EmailSwitch({ current, label, save, onSaved }: {
  current: boolean
  label: ReactNode
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
    <div className={styles.form}>
      <Checkbox checked={current} onChange={(enabled) => void change(enabled)}>
        {label}
      </Checkbox>
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </div>
  )
}
