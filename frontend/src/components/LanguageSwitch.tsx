import { useI18n } from '../i18n/I18n'
import { LOCALES } from '../i18n/locale'
import { TextAction } from './TextAction'
import styles from './LanguageSwitch.module.css'

/**
 * Switches the screens before log-in to the other language, named in that language ("Català" while in English).
 * With two languages one tap is enough; the account page holds the full choice.
 */
export function LanguageSwitch() {
  const { locale, setLocale } = useI18n()
  const other = LOCALES.find((l) => l.value !== locale) ?? LOCALES[0]
  return (
    <span lang={other.value} className={styles.switch}>
      <TextAction onClick={() => setLocale(other.value)}>{other.label}</TextAction>
    </span>
  )
}
