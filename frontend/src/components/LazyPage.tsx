import { Component, Suspense, type ReactNode } from 'react'
import { Screen, ScreenMessage, TopBar } from './Screen'

interface LazyPageProps {
  /** Where the top bar's back goes on the loading and error screens. */
  back: string
  children: ReactNode
}

/** A page from `lazyPage`: a loading screen while its chunk downloads, and a message instead of a blank app if it can't. */
export function LazyPage({ back, children }: LazyPageProps) {
  return (
    <LoadBoundary back={back}>
      <Suspense fallback={<Message back={back}>Loading…</Message>}>{children}</Suspense>
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
    return <Message back={this.props.back}>Couldn't load this page. Reload to try again.</Message>
  }
}

function Message({ back, children }: { back: string; children: ReactNode }) {
  return (
    <Screen top={<TopBar back={back} />}>
      <ScreenMessage>{children}</ScreenMessage>
    </Screen>
  )
}
