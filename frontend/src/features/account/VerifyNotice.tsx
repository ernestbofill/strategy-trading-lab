import { Callout } from '../../components/Callout'
import { useCurrentAccount } from './AccountContext'
import { ResendLink } from './ResendLink'

/** "Check your email" at the top of Pick a template, until the email is confirmed (spec: Sign-up). */
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
