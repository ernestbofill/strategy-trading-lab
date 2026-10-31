import type { SignUpInvite } from '../../api'
import { useT } from '../../i18n/I18n'
import styles from './InviteWelcome.module.css'

/** The greeting on Sign-up when it opens from an invite link: who invited you, what the app is, and your code. */
export function InviteWelcome({ invite }: { invite: SignUpInvite }) {
  const t = useT()
  return (
    <section className={styles.card} aria-labelledby="invite-welcome">
      <p className={`ui-strong ${styles.eyebrow}`}>
        <span aria-hidden="true">✦ </span>
        {t('account.invite.eyebrow')}
      </p>
      <h1 id="invite-welcome" className={styles.title}>
        {t('account.invite.title', { name: invite.firstName })}
      </h1>
      <p className={styles.from}>
        {invite.invitedBy ? t('account.invite.from', { name: invite.invitedBy }) : t('account.invite.anonymous')}
      </p>
      <p className={styles.lead}>{t('account.invite.lead')}</p>
      <p className={styles.code}>
        <span className={styles.codeLabel}>{t('account.invite.code')}</span>
        <span className={styles.codeValue}>{invite.code}</span>
      </p>
    </section>
  )
}
