import { Callout } from '../../components/Callout'
import { useCurrentAccount } from './AccountContext'
import { ResendLink } from './ResendLink'

/** "Check your email" at the top of the dashboard and new-strategy flow until the email is confirmed. */
export function VerifyNotice() {
  const { email, emailVerified } = useCurrentAccount()
  if (emailVerified) return null
  return (
    <Callout tone="info" title="Check your email">
      <p>We sent a link to {email} to confirm it.</p>
      <ResendLink label="Send it again" />
    </Callout>
  )
}
