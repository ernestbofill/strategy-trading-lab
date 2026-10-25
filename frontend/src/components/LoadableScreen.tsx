import type { ReactNode } from 'react'
import type { Loadable } from '../lib/useLoadable'
import { useT } from '../i18n/I18n'
import { Screen, ScreenMessage } from './Screen'

interface LoadableScreenProps<T> {
  state: Loadable<T>
  top: ReactNode
  /** What is loading, for the messages: "the template". */
  what: string
  children: (data: T) => ReactNode
}

/** Shows a screen's loading, error and not-found states, and the screen itself once its data is ready. */
export function LoadableScreen<T>({ state, top, what, children }: LoadableScreenProps<T>) {
  const t = useT()
  if (state.status === 'ready') return children(state.data)
  return (
    <Screen top={top}>
      <ScreenMessage>
        {state.status === 'loading' && t('common.loading', { what })}
        {state.status === 'error' && t('common.loadError', { what })}
        {state.status === 'missing' && t('common.notFound', { what })}
      </ScreenMessage>
    </Screen>
  )
}
