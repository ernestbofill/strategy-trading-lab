import { useState, type FormEvent } from 'react'
import { Navigate, useSearchParams } from 'react-router-dom'
import { api, TooManyTriesError, UnauthorizedError } from '../../api'
import { FormMessage, PasswordField, TextField } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import styles from './Account.module.css'
import { useAccount } from './AccountContext'
import { errorText } from './copy'
import { forgotPasswordPath, safeNext, signUpPath } from './paths'

/** Log-in (spec: Log-in). Afterwards the screen the user was on, or the dashboard. */
export function LogIn() {
  const { state, refresh } = useAccount()
  const next = safeNext(useSearchParams()[0].get('next'))
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  // Logged in (already, or just now): on to where the user was going.
  if (state.status === 'ready' && state.account) return <Navigate to={next} replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setError(null)
    try {
      await api.logIn(email, password)
      await refresh()
    } catch (err) {
      setError(
        err instanceof UnauthorizedError
          ? "That email and password don't match."
          : err instanceof TooManyTriesError
            ? 'Too many tries. Wait a few minutes, or reset your password.'
            : errorText(err),
      )
      setBusy(false)
    }
  }

  return (
    <Screen
      top={<TopBar title="STL // LOG IN" />}
      action={
        <PrimaryAction form="log-in" disabled={busy || !email || !password}>
          {busy ? 'Logging in…' : 'Log in'}
        </PrimaryAction>
      }
    >
      <PageHeader title="Log in" />
      <form id="log-in" className={styles.form} onSubmit={submit} noValidate>
        <TextField label="Email" type="email" autoComplete="email" value={email} onChange={setEmail} />
        <PasswordField label="Password" autoComplete="current-password" value={password} onChange={setPassword} />
        {error && <FormMessage tone="error">{error}</FormMessage>}
      </form>
      <div className={styles.links}>
        <TextAction to={forgotPasswordPath}>Forgot your password?</TextAction>
        <TextAction to={signUpPath}>Create an account</TextAction>
      </div>
    </Screen>
  )
}
