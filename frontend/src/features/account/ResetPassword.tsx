import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { api, ExpiredLinkError, InvalidFieldsError } from '../../api'
import { FormMessage, PasswordField } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { dashboardPath } from '../new-strategy/paths'
import styles from './Account.module.css'
import { useAccount } from './AccountContext'
import { errorText } from './copy'
import { forgotPasswordPath } from './paths'
import { useLinkToken } from './useLinkToken'

/** Choose a new password, from the link in the email. Ends every session, then logs this device in. */
export function ResetPassword() {
  const t = useT()
  const token = useLinkToken()
  const { refresh } = useAccount()
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [linkExpired, setLinkExpired] = useState(false)
  const [busy, setBusy] = useState(false)
  const [done, setDone] = useState(false)

  if (done) return <Navigate to={dashboardPath} replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy || !token || !password) return // Enter while the button is off.
    setBusy(true)
    setError(null)
    try {
      await api.resetPassword(token, password)
      await refresh()
      setDone(true)
    } catch (err) {
      if (err instanceof ExpiredLinkError) setLinkExpired(true)
      else if (err instanceof InvalidFieldsError) setError(err.fields.password ?? err.message)
      else setError(errorText(err))
      setBusy(false)
    }
  }

  const top = <TopBar title={t('account.reset.topBar')} />
  if (!token || linkExpired)
    return (
      <Screen top={top}>
        <PageHeader title={t('account.reset.title')} />
        <FormMessage tone="error">{t('account.expiredLink')}</FormMessage>
        <div className={styles.links}>
          <TextAction to={forgotPasswordPath}>{t('account.reset.forgot')}</TextAction>
        </div>
      </Screen>
    )

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction form="reset-password" disabled={busy || !password}>
          {busy ? t('account.saving') : t('account.reset.submit')}
        </PrimaryAction>
      }
    >
      <PageHeader title={t('account.reset.title')} lead={t('account.reset.lead')} />
      <form id="reset-password" className={styles.form} onSubmit={submit} noValidate>
        <PasswordField
          label={t('account.field.newPassword')}
          autoComplete="new-password"
          value={password}
          onChange={setPassword}
          hint={t('account.passwordHint')}
          error={error}
        />
      </form>
    </Screen>
  )
}
