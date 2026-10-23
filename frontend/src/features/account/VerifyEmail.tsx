import { useEffect, useState } from 'react'
import { api, ExpiredLinkError } from '../../api'
import { FormMessage } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { dashboardPath } from '../new-strategy/paths'
import { useAccount } from './AccountContext'
import { EXPIRED_LINK } from './copy'
import { accountPath, logInPath } from './paths'
import { useLinkToken } from './useLinkToken'

type Result = 'checking' | 'confirmed' | 'expired' | 'failed'

/** One request per link, even when React runs the effect twice (StrictMode): the link works once. */
const verifications = new Map<string, Promise<Result>>()

function verify(token: string): Promise<Result> {
  let request = verifications.get(token)
  if (!request) {
    request = api.verifyEmail(token).then(
      (): Result => 'confirmed',
      (err): Result => {
        if (err instanceof ExpiredLinkError) return 'expired'
        verifications.delete(token) // Not used up: a reload tries again.
        return 'failed'
      },
    )
    verifications.set(token, request)
  }
  return request
}

/** The link in "Confirm your email". It needs no log-in and logs no one in (backend/AUTH.md). */
export function VerifyEmail() {
  const token = useLinkToken()
  const { state, refresh } = useAccount()
  const [result, setResult] = useState<Result>(token ? 'checking' : 'expired')
  const loggedIn = state.status === 'ready' && state.account !== null

  useEffect(() => {
    if (!token) return
    let live = true
    void verify(token).then((r) => live && setResult(r))
    return () => {
      live = false
    }
  }, [token])

  // The logged-in account now reads as confirmed.
  useEffect(() => {
    if (result === 'confirmed' && loggedIn) void refresh()
    // Once, when the result arrives.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [result])

  const action =
    result === 'checking' ? undefined : loggedIn ? (
      <PrimaryAction to={result === 'expired' ? accountPath : dashboardPath}>
        {result === 'expired' ? 'Go to your account' : 'Go to the dashboard'}
      </PrimaryAction>
    ) : (
      <PrimaryAction to={logInPath}>Log in</PrimaryAction>
    )

  return (
    <Screen top={<TopBar title="STL // EMAIL" />} action={action}>
      <PageHeader title={result === 'confirmed' ? 'Your email is confirmed' : 'Confirm your email'} />
      {result === 'checking' && <ScreenMessage>Checking the link…</ScreenMessage>}
      {result === 'expired' && (
        <FormMessage tone="error">
          {EXPIRED_LINK} {loggedIn ? 'Your account page sends it again.' : 'Log in to have it sent again.'}
        </FormMessage>
      )}
      {result === 'failed' && <FormMessage tone="error">Couldn't check the link. Reload to try again.</FormMessage>}
    </Screen>
  )
}
