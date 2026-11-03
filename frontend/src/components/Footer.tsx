import { Link, useLocation } from 'react-router-dom'
import type { Role } from '../api'
import { useT } from '../i18n/I18n'
import type { MessageKey } from '../i18n/messages/en'
import { useRole } from '../features/account/AccountContext'
import { accountPath } from '../features/dashboard/paths'
import { accountNotificationsPath, logInPath, signUpPath } from '../features/account/paths'
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

/** Narrow screens: one App group, so the footer stays short. */
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

/** Wide screens (from 1024px): the account and the welcome page get their own columns. */
const discover: LinkGroup = { title: 'footer.discover', links: [{ to: welcomePath, label: 'footer.welcome' }] }

const guestWide: LinkGroup[] = [
  { title: 'footer.app', links: [{ to: pickPath, label: 'footer.templates' }] },
  {
    title: 'footer.account',
    links: [
      { to: signUpPath, label: 'footer.signUp' },
      { to: logInPath, label: 'footer.logIn' },
    ],
  },
  discover,
]

const userWide: LinkGroup[] = [
  {
    title: 'footer.app',
    links: [
      { to: homePath, label: 'footer.dashboard' },
      { to: pickPath, label: 'footer.newStrategy' },
    ],
  },
  {
    title: 'footer.account',
    links: [
      { to: accountPath, label: 'footer.settings' },
      { to: accountNotificationsPath, label: 'footer.notifications' },
    ],
  },
  discover,
]

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

/** The links this user may open: a guest's, a signed-in user's, plus the admin pages for an owner. Only Legal while the account loads. */
function groupsFor(role: Role | null | undefined, wide: boolean): LinkGroup[] {
  if (role === undefined) return [legal]
  if (role === null) return wide ? [...guestWide, legal] : [guest, legal]
  const own = wide ? userWide : [user]
  return role === 'owner' ? [...own, owner, legal] : [...own, legal]
}

function SiteMap({ groups, className }: { groups: LinkGroup[]; className: string }) {
  const t = useT()
  const { pathname, hash } = useLocation()
  return (
    <nav aria-label={t('footer.siteMap')} className={className}>
      {groups.map((group) => (
        <section key={group.title} aria-label={t(group.title)}>
          <h2 className={styles.heading}>{t(group.title)}</h2>
          <ul className={styles.links}>
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
        </section>
      ))}
    </nav>
  )
}

/**
 * The site footer: a site map of every page the user may open, then the short disclaimer. Two maps are rendered and CSS
 * shows one: the narrow one (one App group) below 1024px, the wide one (Account and Welcome in their own columns) from there.
 */
export function Footer() {
  const t = useT()
  const role = useRole()

  return (
    <footer className={styles.footer}>
      <SiteMap groups={groupsFor(role, false)} className={`${styles.nav} ${styles.narrow}`} />
      <SiteMap groups={groupsFor(role, true)} className={`${styles.nav} ${styles.wide}`} />
      <p className={styles.disclaimer}>{t('common.disclaimer')}</p>
    </footer>
  )
}
