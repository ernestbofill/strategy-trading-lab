import { NavLink } from 'react-router-dom'
import type { Role } from '../api'
import { useT } from '../i18n/I18n'
import type { MessageKey } from '../i18n/messages/en'
import { useRole } from '../features/account/AccountContext'
import { accountPath } from '../features/dashboard/paths'
import { logInPath, signUpPath } from '../features/account/paths'
import { aboutPath, disclaimerPath, legalNoticePath, privacyPath, termsPath } from '../features/legal/paths'
import { homePath, pickPath } from '../features/new-strategy/paths'
import { welcomePath } from '../features/welcome/paths'
import styles from './Footer.module.css'

interface SiteLink {
  to: string
  label: MessageKey
}

interface LinkGroup {
  title: MessageKey
  links: SiteLink[]
}

const legal: LinkGroup = {
  title: 'legal.nav',
  links: [
    { to: legalNoticePath, label: 'legal.notice.title' },
    { to: termsPath, label: 'legal.terms.title' },
    { to: privacyPath, label: 'legal.privacy.title' },
    { to: disclaimerPath, label: 'legal.disclaimer.title' },
    { to: aboutPath, label: 'legal.about.title' },
  ],
}

const guest: LinkGroup = {
  title: 'footer.app',
  links: [
    { to: welcomePath, label: 'footer.welcome' },
    { to: pickPath, label: 'footer.templates' },
    { to: signUpPath, label: 'footer.signUp' },
    { to: logInPath, label: 'footer.logIn' },
  ],
}

const user: LinkGroup = {
  title: 'footer.app',
  links: [
    { to: homePath, label: 'footer.dashboard' },
    { to: pickPath, label: 'footer.newStrategy' },
    { to: accountPath, label: 'footer.account' },
    { to: welcomePath, label: 'footer.welcome' },
  ],
}

const owner: LinkGroup = {
  title: 'footer.admin',
  links: [
    { to: '/admin', label: 'footer.overview' },
    { to: '/admin/alerts', label: 'footer.alerts' },
    { to: '/admin/users', label: 'footer.users' },
    { to: '/admin/assets', label: 'footer.assets' },
    { to: '/admin/invites', label: 'footer.invites' },
    { to: '/feedback', label: 'footer.feedback' },
  ],
}

/** The links this user may open: a guest's, a signed-in user's, plus the admin pages for an owner. None while the account loads. */
function groupsFor(role: Role | null | undefined): LinkGroup[] {
  if (role === undefined) return [legal]
  if (role === null) return [guest, legal]
  return role === 'owner' ? [user, owner, legal] : [user, legal]
}

/** The site footer: a site map of every page the user may open, then the short disclaimer. */
export function Footer() {
  const t = useT()
  const groups = groupsFor(useRole())

  return (
    <footer className={styles.footer}>
      <p className={styles.brand}>STL // STRATEGY TRADING LAB</p>
      <nav aria-label={t('footer.siteMap')} className={styles.nav}>
        {groups.map((group) => (
          <section key={group.title} className={styles.group} aria-labelledby={`footer-${group.title}`}>
            <h2 id={`footer-${group.title}`} className={styles.heading}>
              {t(group.title)}
            </h2>
            <ul className={styles.links}>
              {group.links.map(({ to, label }) => (
                <li key={to}>
                  {/* `end`: only the page itself is current, not the pages under it (Admin vs Alerts). */}
                  <NavLink to={to} end className={styles.link}>
                    {t(label)}
                  </NavLink>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </nav>
      <p className={styles.disclaimer}>{t('common.disclaimer')}</p>
    </footer>
  )
}
