import { Callout } from '../../components/Callout'
import { useT } from '../../i18n/I18n'
import { useOptionalAccount } from './AccountContext'
import { ResendLink } from './ResendLink'

/** "Check your email" at the top of the dashboard and new-strategy flow until the email is confirmed. */
export function VerifyNotice() {
  const t = useT()
  const account = useOptionalAccount()
  if (!account || account.emailVerified) return null
  const { email } = account
  return (
    <Callout tone="info" title={t('account.notice.title')}>
      <p>{t('account.notice.body', { email })}</p>
      <ResendLink label={t('account.notice.resend')} />
    </Callout>
  )
}
