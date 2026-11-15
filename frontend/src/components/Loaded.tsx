import type { ReactNode } from 'react'
import { useT } from '../i18n/I18n'
import type { Loadable } from '../lib/useLoadable'
import { ScreenMessage } from './Screen'

interface LoadMessageProps {
  state: Exclude<Loadable<unknown>, { status: 'ready' }>
  what: string
  /** The admin pages are in English only: their line too, whatever the app's language. */
  english?: boolean
}

/** The line for data that isn't ready: loading, couldn't load, or not found. */
export function LoadMessage({ state, what, english }: LoadMessageProps) {
  const t = useT()
  const line = english
    ? { loading: `Loading ${what}…`, error: `Could not load ${what}. Try again later.`, missing: `We couldn't find ${what}.` }[state.status]
    : t(state.status === 'loading' ? 'common.loading' : state.status === 'error' ? 'common.loadError' : 'common.notFound', { what })
  return <ScreenMessage>{line}</ScreenMessage>
}

interface LoadedProps<T> {
  state: Loadable<T>
  /** What is loading, for the messages: "the users". */
  what: string
  english?: boolean
  children: (data: T) => ReactNode
}

/**
 * Data a screen loads, once ready; until then its loading, error or not-found line. For a part of a screen that shows its
 * header first (the admin pages); a whole screen uses `LoadableScreen`.
 */
export function Loaded<T>({ state, what, english, children }: LoadedProps<T>) {
  return state.status === 'ready' ? children(state.data) : <LoadMessage state={state} what={what} english={english} />
}
