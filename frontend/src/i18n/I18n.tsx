import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import type { Locale } from '../api/types'
import { invalidateAll } from '../lib/useLoadable'
import { formatMessage, type MessageParams } from './format'
import { currentLocale, DEFAULT_LOCALE, setCurrentLocale, storedLocale, storeLocale } from './locale'
import { ca } from './messages/ca'
import { en, type MessageKey } from './messages/en'

const messages: Record<Locale, Record<MessageKey, string>> = { en, ca }

// eslint-disable-next-line react-refresh/only-export-components
export type Translate = (key: MessageKey, params?: MessageParams) => string

// eslint-disable-next-line react-refresh/only-export-components
export const translate = (locale: Locale, key: MessageKey, params?: MessageParams) =>
  formatMessage(messages[locale][key] ?? en[key], params, locale)

// eslint-disable-next-line react-refresh/only-export-components
export const tNow: Translate = (key, params) => translate(currentLocale(), key, params)

interface I18nValue {
  locale: Locale
  /**
   * Switches the page's language. Loads cached for the old language (the templates' words come from the API) are
   * forgotten, so the screens ask again. The account's saved language is a separate call (`api.setLocale`).
   */
  setLocale: (locale: Locale) => void
  t: Translate
}

const I18nContext = createContext<I18nValue>({
  locale: DEFAULT_LOCALE,
  setLocale: () => {},
  t: (key, params) => translate(DEFAULT_LOCALE, key, params),
})

/** The app's language: this device's last choice (English on a new device), then the account's once it is known. */
export function I18nProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(() => {
    const initial = storedLocale()
    setCurrentLocale(initial)
    return initial
  })

  const setLocale = useCallback(
    (next: Locale) => {
      if (next === currentLocale()) return
      setCurrentLocale(next)
      storeLocale(next)
      invalidateAll()
      setLocaleState(next)
    },
    [],
  )

  const value = useMemo<I18nValue>(
    () => ({ locale, setLocale, t: (key, params) => translate(locale, key, params) }),
    [locale, setLocale],
  )
  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export const useI18n = () => useContext(I18nContext)

/** `const t = useT()`, then `t('account.title')`. */
// eslint-disable-next-line react-refresh/only-export-components
export const useT = (): Translate => useContext(I18nContext).t
