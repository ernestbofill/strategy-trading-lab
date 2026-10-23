import { useState, type FormEvent } from 'react'
import { api } from '../../api'
import { FormMessage, TextField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import styles from './Account.module.css'
import { errorText } from './copy'
import { logInPath } from './paths'

/** Forgot your password? Always the same answer, whether or not the email has an account. */
export function ForgotPassword() {
  const [email, setEmail] = useState('')
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setMessage(null)
    try {
      await api.forgotPassword(email)
      setMessage({ tone: 'done', text: 'If that email has an account, we sent a link. It works for 1 hour.' })
    } catch (err) {
      setMessage({ tone: 'error', text: errorText(err) })
    }
    setBusy(false)
  }

  return (
    <Screen
      top={<TopBar title="STL // PASSWORD" back={logInPath} />}
      action={
        <PrimaryAction form="forgot-password" disabled={busy || !email}>
          {busy ? 'Sending…' : 'Send the link'}
        </PrimaryAction>
      }
    >
      <PageHeader title="Forgot your password?" lead="We'll email you a link to choose a new one." />
      <form id="forgot-password" className={styles.form} onSubmit={submit} noValidate>
        <TextField label="Email" type="email" autoComplete="email" value={email} onChange={setEmail} />
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      </form>
      <div className={styles.links}>
        <TextAction to={logInPath}>Log in</TextAction>
      </div>
    </Screen>
  )
}
