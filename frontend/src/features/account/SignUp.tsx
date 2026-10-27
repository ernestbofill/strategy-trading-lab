import { useMemo, useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { api, InvalidFieldsError, type Currency, type Locale } from '../../api'
import { Checkbox, FormMessage, PasswordField, SegmentedField, SelectField, TextField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useI18n, useT } from '../../i18n/I18n'
import { LOCALES } from '../../i18n/locale'
import { currencyOptionsWith } from './currencies'
import { dashboardPath, pickPath } from '../new-strategy/paths'
import styles from './Account.module.css'
import { useAccount } from './AccountContext'
import { errorText } from './copy'
import { logInPath } from './paths'
import { guessTimeZone, timeZones } from './timeZones'

/** Sign-up (spec: Sign-up). A new account is logged in at once and goes to Pick a template. */
export function SignUp() {
  const t = useT()
  const { locale: currentAppLocale, setLocale: setAppLocale } = useI18n()
  const { state, refresh } = useAccount()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [locale, setLocale] = useState<Locale>(currentAppLocale)
  const [currency, setCurrency] = useState<Currency>('EUR')
  const [timeZone, setTimeZone] = useState(guessTimeZone)
  const [overviewEmail, setOverviewEmail] = useState(true)
  const [accepted, setAccepted] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)
  const [signedUp, setSignedUp] = useState(false)
  // Built once: the list holds the guessed zone, and any zone picked comes from it.
  const zones = useMemo(() => timeZones(guessTimeZone()).map((z) => ({ value: z, label: z.replaceAll('_', ' ') })), [])
  const currOptions = useMemo(() => currencyOptionsWith(t), [t])

  if (state.status === 'ready' && state.account) return <Navigate to={signedUp ? pickPath : dashboardPath} replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy || !accepted) return // Enter while the button is off.
    setBusy(true)
    setErrors({})
    setMessage(null)
    try {
      await api.signUp({ email, password, locale, timeZone, currency, overviewEmail, disclaimerAccepted: accepted })
      setSignedUp(true)
      // A new account is logged in now; an email that already had one isn't, and gets the same answer.
      const account = await refresh()
      if (account === null) setMessage({ tone: 'done', text: t('account.signUp.checkEmail') })
      else if (account === undefined) setMessage({ tone: 'error', text: errorText(null) })
    } catch (err) {
      if (err instanceof InvalidFieldsError) setErrors(err.fields)
      else setMessage({ tone: 'error', text: errorText(err) })
    }
    setBusy(false)
  }

  const onLocaleChange = (v: string) => {
    const next = v as Locale
    setLocale(next)
    setAppLocale(next)
  }

  return (
    <Screen
      top={<TopBar title={t('account.signUp.topBar')} />}
      action={
        <PrimaryAction form="sign-up" disabled={busy || !accepted}>
          {busy ? t('account.signUp.submitting') : t('account.signUp.submit')}
        </PrimaryAction>
      }
      note={accepted ? undefined : t('account.signUp.note')}
    >
      <PageHeader title={t('account.signUp.title')} lead={t('account.signUp.lead')} />
      <form id="sign-up" className={styles.form} onSubmit={submit} noValidate>
        <TextField label={t('account.field.email')} type="email" autoComplete="username" value={email} onChange={setEmail} error={errors.email} />
        <PasswordField
          label={t('account.field.password')}
          autoComplete="new-password"
          value={password}
          onChange={setPassword}
          hint={t('account.passwordHint')}
          error={errors.password}
        />
        <SegmentedField
          label={t('account.signUp.language')}
          value={locale}
          onChange={onLocaleChange}
          options={LOCALES}
          error={errors.locale}
        />
        <SegmentedField
          label={t('account.signUp.currency')}
          value={currency}
          onChange={(v) => setCurrency(v as Currency)}
          options={currOptions}
          error={errors.currency}
        />
        <SelectField label={t('account.signUp.timeZone')} value={timeZone} onChange={setTimeZone} options={zones} error={errors.timeZone} />
        <Checkbox checked={overviewEmail} onChange={setOverviewEmail}>
          {t('account.overviewEmail')}
        </Checkbox>
        <Checkbox checked={accepted} onChange={setAccepted} error={errors.disclaimerAccepted}>
          {t('account.signUp.accept', { disclaimer: t('common.disclaimerFull') })}
        </Checkbox>
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      </form>
      <div className={styles.links}>
        <TextAction to={logInPath}>{t('account.signUp.haveAccount')}</TextAction>
      </div>
    </Screen>
  )
}
