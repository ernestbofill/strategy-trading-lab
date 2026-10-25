import { useState, type FormEvent } from 'react'
import { api } from '../../api'
import { FormMessage, TextField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import styles from './Account.module.css'
import { errorText } from './copy'
import { logInPath } from './paths'

/** Forgot your password? Always the same answer, whether or not the email has an account. */
export function ForgotPassword() {
  const t = useT()
  const [email, setEmail] = useState('')
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy || !email) return // Enter while the button is off.
    setBusy(true)
    setMessage(null)
    try {
      await api.forgotPassword(email)
      setMessage({ tone: 'done', text: t('account.forgot.sent') })
    } catch (err) {
      setMessage({ tone: 'error', text: errorText(err) })
    }
    setBusy(false)
  }

  return (
    <Screen
      top={<TopBar title={t('account.forgot.topBar')} back={logInPath} />}
      action={
        <PrimaryAction form="forgot-password" disabled={busy || !email}>
          {busy ? t('account.forgot.submitting') : t('account.forgot.submit')}
        </PrimaryAction>
      }
    >
      <PageHeader title={t('account.forgot.title')} lead={t('account.forgot.lead')} />
      <form id="forgot-password" className={styles.form} onSubmit={submit} noValidate>
        <TextField label={t('account.field.email')} type="email" autoComplete="email" value={email} onChange={setEmail} />
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      </form>
      <div className={styles.links}>
        <TextAction to={logInPath}>{t('account.forgot.logIn')}</TextAction>
      </div>
    </Screen>
  )
}
