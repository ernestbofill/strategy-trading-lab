import { useMemo, useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { api, InvalidFieldsError, type Currency, type Locale } from '../../api'
import { Checkbox, FormMessage, PasswordField, SelectField, TextField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { DISCLAIMER_FULL } from '../../lib/copy'
import { currencyOptions } from './currencies'
import { dashboardPath, pickPath } from '../new-strategy/paths'
import styles from './Account.module.css'
import { useAccount } from './AccountContext'
import { errorText } from './copy'
import { logInPath } from './paths'
import { guessTimeZone, timeZones } from './timeZones'

const languages: { value: Locale; label: string }[] = [
  { value: 'en', label: 'English' },
  { value: 'ca', label: 'Català' },
]

/** Sign-up (spec: Sign-up). A new account is logged in at once and goes on to Pick a template. */
export function SignUp() {
  const { state, refresh } = useAccount()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [locale, setLocale] = useState<Locale>('en')
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
      if (account === null) setMessage({ tone: 'done', text: 'Check your email.' })
      else if (account === undefined) setMessage({ tone: 'error', text: errorText(null) })
    } catch (err) {
      if (err instanceof InvalidFieldsError) setErrors(err.fields)
      else setMessage({ tone: 'error', text: errorText(err) })
    }
    setBusy(false)
  }

  return (
    <Screen
      top={<TopBar title="STL // SIGN UP" />}
      action={
        <PrimaryAction form="sign-up" disabled={busy || !accepted}>
          {busy ? 'Creating it…' : 'Create my account'}
        </PrimaryAction>
      }
      note={accepted ? undefined : 'Tick the box above to go on.'}
    >
      <PageHeader title="Create your account" lead="Follow strategies with your own rules. The app never trades for you." />
      <form id="sign-up" className={styles.form} onSubmit={submit} noValidate>
        <TextField label="Email" type="email" autoComplete="username" value={email} onChange={setEmail} error={errors.email} />
        <PasswordField
          label="Password"
          autoComplete="new-password"
          value={password}
          onChange={setPassword}
          hint="At least 10 characters."
          error={errors.password}
        />
        <SelectField
          label="Language"
          value={locale}
          onChange={(v) => setLocale(v as Locale)}
          options={languages}
          error={errors.locale}
        />
        <SelectField
          label="Currency"
          value={currency}
          onChange={(v) => setCurrency(v as Currency)}
          options={currencyOptions}
          error={errors.currency}
        />
        <SelectField label="Time zone" value={timeZone} onChange={setTimeZone} options={zones} error={errors.timeZone} />
        <Checkbox checked={overviewEmail} onChange={setOverviewEmail}>
          Email me a daily overview, early in the morning.
        </Checkbox>
        <Checkbox checked={accepted} onChange={setAccepted} error={errors.disclaimerAccepted}>
          {DISCLAIMER_FULL} The app is for learning, testing and following strategies; it never trades for you.
        </Checkbox>
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      </form>
      <div className={styles.links}>
        <TextAction to={logInPath}>I already have an account</TextAction>
      </div>
    </Screen>
  )
}
