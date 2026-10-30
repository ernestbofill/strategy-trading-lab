import { Link } from 'react-router-dom'
import { useT } from '../i18n/I18n'
import type { MessageKey } from '../i18n/messages/en'
import { aboutPath, disclaimerPath, legalNoticePath, privacyPath, termsPath } from '../features/legal/paths'
import styles from './Footer.module.css'

const links: { to: string; label: MessageKey }[] = [
  { to: legalNoticePath, label: 'legal.notice.title' },
  { to: termsPath, label: 'legal.terms.title' },
  { to: privacyPath, label: 'legal.privacy.title' },
  { to: disclaimerPath, label: 'legal.disclaimer.title' },
  { to: aboutPath, label: 'legal.about.title' },
]

export function Footer() {
  const t = useT()

  return (
    <footer className={styles.footer}>
      <p className={styles.disclaimer}>{t('common.disclaimer')}</p>
      <nav aria-label={t('legal.nav')} className={styles.nav}>
        <ul className={styles.links}>
          {links.map(({ to, label }) => (
            <li key={to}>
              <Link to={to} className={styles.link}>
                {t(label)}
              </Link>
            </li>
          ))}
        </ul>
      </nav>
    </footer>
  )
}
