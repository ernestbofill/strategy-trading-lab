import type { Locale } from '../api/types'
import { readDeviceChoice, storeDeviceChoice } from '../lib/deviceStorage'

/** The languages the app is written in. Each is the name the language gives itself, so a user finds theirs in any language. */
export const LOCALES: { value: Locale; label: string }[] = [
  { value: 'en', label: 'English' },
  { value: 'ca', label: 'Català' },
]

/** English is the default and is never guessed from the browser (spec: Language). */
export const DEFAULT_LOCALE: Locale = 'en'

const STORAGE_KEY = 'stl.locale'

export const isLocale = (value: unknown): value is Locale => LOCALES.some((l) => l.value === value)

/** The language this device last used: it is what the screens before log-in speak. Always English on a new device. */
export const storedLocale = (): Locale => readDeviceChoice(STORAGE_KEY, isLocale, DEFAULT_LOCALE)

export const storeLocale = (locale: Locale) => storeDeviceChoice(STORAGE_KEY, locale)

/** The language of the page now, which the API request headers and the formatters read outside React. */
let current: Locale = DEFAULT_LOCALE

export const currentLocale = () => current

export function setCurrentLocale(locale: Locale) {
  current = locale
  document.documentElement.lang = locale
}
