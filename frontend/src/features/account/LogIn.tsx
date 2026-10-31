import { useState, type FormEvent } from 'react'
import { Navigate, useSearchParams } from 'react-router-dom'
import { api, TooManyTriesError, UnauthorizedError } from '../../api'
import { FormMessage, PasswordField, TextField } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import styles from './Account.module.css'
import { useAccount } from './AccountContext'
import { errorText } from './copy'
import { forgotPasswordPath, safeNext, signUpPath, signUpPathFor } from './paths'

/** Log-in (spec: Log-in). Afterwards the screen the user was on, or the dashboard. */
export function LogIn() {
  const t = useT()
  const { state, refresh } = useAccount()
  const nextParam = useSearchParams()[0].get('next')
  const next = safeNext(nextParam)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  // Logged in (already, or just now): on to where the user was going.
  if (state.status === 'ready' && state.account) return <Navigate to={next} replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy || !email || !password) return // Enter while the button is off.
    setBusy(true)
    setError(null)
    try {
      await api.logIn(email, password)
      // Logged in: the redirect above takes over once the account is read; if it can't be, say so.
      if (!(await refresh())) {
        setError(errorText(null))
        setBusy(false)
      }
    } catch (err) {
      setError(
        err instanceof UnauthorizedError
          ? t('account.logIn.mismatch')
          : err instanceof TooManyTriesError
            ? t('account.logIn.tooManyTries')
            : errorText(err),
      )
      setBusy(false)
    }
  }

  return (
    <Screen
      top={<TopBar title={t('account.logIn.topBar')} />}
      action={
        <PrimaryAction form="log-in" disabled={busy || !email || !password}>
          {busy ? t('account.logIn.submitting') : t('account.logIn.submit')}
        </PrimaryAction>
      }
    >
      <PageHeader title={t('account.logIn.title')} />
      <form id="log-in" className={styles.form} onSubmit={submit} noValidate>
        <TextField label={t('account.field.email')} type="email" autoComplete="username" value={email} onChange={setEmail} />
        <PasswordField label={t('account.field.password')} autoComplete="current-password" value={password} onChange={setPassword} />
        {error && <FormMessage tone="error">{error}</FormMessage>}
      </form>
      <div className={styles.links}>
        <TextAction to={forgotPasswordPath}>{t('account.logIn.forgot')}</TextAction>
        <TextAction to={nextParam ? signUpPathFor(nextParam) : signUpPath}>{t('account.logIn.create')}</TextAction>
      </div>
    </Screen>
  )
}
