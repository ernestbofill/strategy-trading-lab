import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { useAccount } from './AccountContext'
import { hasLoggedInHere } from './knownDevice'
import { TextAction } from '../../components/TextAction'
import { logInPathFor, signUpPath } from './paths'

/**
 * The layout route of the app's screens, which need a logged-in user. Without one: Log-in, coming back here after (or Sign-up on a device that
 * has never logged in). A session that ends on a screen (a 401) lands here too.
 */
export function RequireAccount() {
  const { state, refresh } = useAccount()
  const { pathname, search } = useLocation()

  if (state.status === 'loading')
    return (
      <Screen top={<TopBar title="STL" />}>
        <ScreenMessage>Loading…</ScreenMessage>
      </Screen>
    )
  if (state.status === 'error')
    return (
      <Screen top={<TopBar title="STL" />}>
        <ScreenMessage>
          Could not reach Strategy Trading Lab.{' '}
          <TextAction onClick={() => void refresh()}>Try again</TextAction>
        </ScreenMessage>
      </Screen>
    )
  if (!state.account) {
    const from = `${pathname}${search}`
    return <Navigate to={hasLoggedInHere() || from !== '/' ? logInPathFor(from) : signUpPath} replace />
  }
  return <Outlet />
}
