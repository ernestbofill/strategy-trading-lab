import { Link, useLocation } from 'react-router-dom'
import type { Role } from '../api'
import { useT } from '../i18n/I18n'
import type { MessageKey } from '../i18n/messages/en'
import { useRole } from '../features/account/AccountContext'
import { accountPath, settingsPath } from '../features/dashboard/paths'
import { settingsNotificationsPath, logInPath, signUpPath } from '../features/account/paths'
import { aboutPath, disclaimerPath, legalNoticePath, privacyPath, termsPath } from '../features/legal/paths'
import { homePath, pickPath } from '../features/new-strategy/paths'
import { welcomePath } from '../features/welcome/paths'
import { useBackendVersion } from '../lib/backendVersion'
import styles from './Footer.module.css'
import { Tag } from './Tag'

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

const guest: LinkGroup[] = [
  {
    title: 'footer.app',
    links: [
      { to: welcomePath, label: 'footer.welcome' },
      { to: pickPath, label: 'footer.templates' },
    ],
  },
  {
    title: 'footer.account',
    links: [
      { to: signUpPath, label: 'footer.signUp' },
      { to: logInPath, label: 'footer.logIn' },
    ],
  },
]

const user: LinkGroup[] = [
  {
    title: 'footer.app',
    links: [
      { to: welcomePath, label: 'footer.welcome' },
      { to: homePath, label: 'footer.dashboard' },
      { to: pickPath, label: 'footer.newStrategy' },
    ],
  },
  {
    title: 'footer.settings',
    links: [
      { to: settingsPath, label: 'footer.settings' },
      { to: accountPath, label: 'footer.account' },
      { to: settingsNotificationsPath, label: 'footer.notifications' },
    ],
  },
]

const owner: LinkGroup = {
  title: 'footer.admin',
  links: [
    { to: '/admin', label: 'footer.admin' },
    { to: '/admin/alerts', label: 'footer.alerts' },
    { to: '/admin/users', label: 'footer.users' },
    { to: '/admin/assets', label: 'footer.assets' },
    { to: '/admin/invites', label: 'footer.invites' },
    { to: '/admin/templates', label: 'footer.adminTemplates' },
    { to: '/feedback', label: 'footer.feedback' },
  ],
}

/** The links this user may open: a guest's, a signed-in user's, plus the admin pages for an owner. Only Legal while the account loads. */
function groupsFor(role: Role | null | undefined): LinkGroup[] {
  if (role === undefined) return [legal]
  if (role === null) return [...guest, legal]
  return role === 'owner' ? [...user, owner, legal] : [...user, legal]
}

/** The site footer: every page the user may open, in columns without headings (the title only names a column for screen readers), then the short disclaimer and the app's version. */
export function Footer() {
  const t = useT()
  const groups = groupsFor(useRole())
  const { pathname, hash } = useLocation()
  const onDevDatabase = useBackendVersion()?.database === 'dev'
  return (
    <footer className={styles.footer}>
      <nav aria-label={t('footer.siteMap')} className={styles.nav}>
        {groups.map((group) => (
          <ul key={group.title} aria-label={t(group.title)} className={styles.links}>
            {group.links.map(({ to, label }) => {
              // Only the page itself is current (Admin, not under Alerts), and a section link (#notifications) only on its section.
              const [path, section = ''] = to.split('#')
              const current = pathname === path && hash === (section && `#${section}`)
              return (
                <li key={to}>
                  <Link to={to} className={styles.link} aria-current={current ? 'page' : undefined}>
                    {t(label)}
                  </Link>
                </li>
              )
            })}
          </ul>
        ))}
      </nav>
      <div className={styles.bottom}>
        <p className={styles.disclaimer}>{t('common.disclaimer')}</p>
        <p className={styles.version}>
          v{__APP_VERSION__}
          {onDevDatabase && (
            <Tag tone="dev" title={t('common.devDatabase')}>
              DEV
            </Tag>
          )}
        </p>
      </div>
    </footer>
  )
}
