import { Link, useLocation } from 'react-router-dom'
import { useT } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import { aboutPath, disclaimerPath, legalNoticePath, privacyPath, termsPath } from './paths'
import styles from './LegalNav.module.css'

interface Tab {
  to: string
  label: MessageKey
}

const tabs: Tab[] = [
  { to: legalNoticePath, label: 'legal.notice.title' },
  { to: termsPath, label: 'legal.terms.title' },
  { to: privacyPath, label: 'legal.privacy.title' },
  { to: disclaimerPath, label: 'legal.disclaimer.title' },
  { to: aboutPath, label: 'legal.about.title' },
]

export function LegalNav() {
  const t = useT()
  const { pathname } = useLocation()

  return (
    <nav className={styles.nav} aria-label={t('legal.nav')}>
      {tabs.map((tab) => (
        <Link
          key={tab.to}
          to={tab.to}
          className={styles.tab}
          aria-current={pathname === tab.to ? 'page' : undefined}
        >
          {t(tab.label)}
        </Link>
      ))}
    </nav>
  )
}
