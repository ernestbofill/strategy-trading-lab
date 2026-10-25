import { useEffect, useState } from 'react'
import { api, ExpiredLinkError } from '../../api'
import { FormMessage } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { useT } from '../../i18n/I18n'
import { dashboardPath } from '../new-strategy/paths'
import { useAccount } from './AccountContext'
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
  const t = useT()
  const token = useLinkToken()
  const { state, refresh } = useAccount()
  const [verified, setVerified] = useState<Result>('checking')
  const result: Result = token ? verified : 'expired'
  const loggedIn = state.status === 'ready' && state.account !== null

  useEffect(() => {
    if (!token) return
    let live = true
    void verify(token).then((r) => {
      if (!live) return
      setVerified(r)
      if (r === 'confirmed') void refresh() // A logged-in account now reads as confirmed.
    })
    return () => {
      live = false
    }
  }, [token, refresh])

  const [to, label] = !loggedIn
    ? [logInPath, t('account.verify.logIn')]
    : result === 'expired'
      ? [accountPath, t('account.verify.goAccount')]
      : [dashboardPath, t('account.verify.goDashboard')]
  const action = result === 'checking' ? undefined : <PrimaryAction to={to}>{label}</PrimaryAction>

  return (
    <Screen top={<TopBar title={t('account.verify.topBar')} />} action={action}>
      <PageHeader title={result === 'confirmed' ? t('account.verify.titleConfirmed') : t('account.verify.title')} />
      {result === 'checking' && <ScreenMessage>{t('account.verify.checking')}</ScreenMessage>}
      {result === 'expired' && (
        <FormMessage tone="error">
          {t('account.expiredLink')} {loggedIn ? t('account.verify.expiredLoggedIn') : t('account.verify.expiredLoggedOut')}
        </FormMessage>
      )}
      {result === 'failed' && <FormMessage tone="error">{t('account.verify.failed')}</FormMessage>}
    </Screen>
  )
}
