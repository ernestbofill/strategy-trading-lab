import { Component, Suspense, type ReactNode } from 'react'
import { Screen, ScreenMessage, TopBar } from './Screen'

interface LazyPageProps {
  children: ReactNode
}

/** A page from `lazyPage` (the admin pages): a loading screen while its chunk downloads, and a message instead of a blank app if it can't. */
export function LazyPage({ children }: LazyPageProps) {
  return (
    <LoadBoundary>
      <Suspense fallback={<Message>Loading…</Message>}>{children}</Suspense>
    </LoadBoundary>
  )
}

class LoadBoundary extends Component<LazyPageProps, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  render() {
    if (!this.state.failed) return this.props.children
    return <Message>Couldn't load this page. Reload to try again.</Message>
  }
}

function Message({ children }: { children: ReactNode }) {
  return (
    <Screen top={<TopBar home />}>
      <ScreenMessage>{children}</ScreenMessage>
    </Screen>
  )
}
