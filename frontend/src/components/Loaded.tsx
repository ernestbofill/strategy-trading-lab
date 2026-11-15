import type { ReactNode } from 'react'
import { useT } from '../i18n/I18n'
import type { Loadable } from '../lib/useLoadable'
import { ScreenMessage } from './Screen'

/** The line for data that isn't ready: loading, couldn't load, or not found. */
export function LoadMessage({ state, what }: { state: Exclude<Loadable<unknown>, { status: 'ready' }>; what: string }) {
  const t = useT()
  return (
    <ScreenMessage>
      {state.status === 'loading' && t('common.loading', { what })}
      {state.status === 'error' && t('common.loadError', { what })}
      {state.status === 'missing' && t('common.notFound', { what })}
    </ScreenMessage>
  )
}

interface LoadedProps<T> {
  state: Loadable<T>
  /** What is loading, for the messages: "the users". */
  what: string
  children: (data: T) => ReactNode
}

/**
 * Data a screen loads, once ready; until then its loading, error or not-found line. For a part of a screen that shows its
 * header first (the admin pages); a whole screen uses `LoadableScreen`.
 */
export function Loaded<T>({ state, what, children }: LoadedProps<T>) {
  return state.status === 'ready' ? children(state.data) : <LoadMessage state={state} what={what} />
}
