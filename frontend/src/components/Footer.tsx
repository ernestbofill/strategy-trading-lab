import { Link } from 'react-router-dom'
import { useT } from '../i18n/I18n'
import { aboutPath, disclaimerPath, legalNoticePath, privacyPath, termsPath } from '../features/legal/paths'
import styles from './Footer.module.css'

export function Footer() {
  const t = useT()

  return (
    <footer className={styles.footer}>
      <p className={styles.disclaimer}>{t('common.disclaimer')}</p>
      <nav aria-label={t('legal.nav')} className={styles.nav}>
        <ul className={styles.links}>
          <li>
            <Link to={legalNoticePath} className={styles.link}>
              {t('legal.notice.title')}
            </Link>
          </li>
          <li>
            <Link to={termsPath} className={styles.link}>
              {t('legal.terms.title')}
            </Link>
          </li>
          <li>
            <Link to={privacyPath} className={styles.link}>
              {t('legal.privacy.title')}
            </Link>
          </li>
          <li>
            <Link to={disclaimerPath} className={styles.link}>
              {t('legal.disclaimer.title')}
            </Link>
          </li>
          <li>
            <Link to={aboutPath} className={styles.link}>
              {t('legal.about.title')}
            </Link>
          </li>
        </ul>
      </nav>
    </footer>
  )
}
