import type { ReactNode } from 'react'
import { useT } from '../i18n/I18n'
import type { Loadable } from '../lib/useLoadable'
import { ScreenMessage } from './Screen'

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
  const t = useT()
  if (state.status === 'ready') return children(state.data)
  return (
    <ScreenMessage>
      {state.status === 'loading' && t('common.loading', { what })}
      {state.status === 'error' && t('common.loadError', { what })}
      {state.status === 'missing' && t('common.notFound', { what })}
    </ScreenMessage>
  )
}
