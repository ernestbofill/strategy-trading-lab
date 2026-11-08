import { Link } from 'react-router-dom'
import { LanguageSwitch } from '../../components/LanguageSwitch'
import { Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import { useRole } from '../account/AccountContext'
import { logInPath, signUpPath } from '../account/paths'
import { pickPath } from '../new-strategy/paths'
import { InteractiveFeatures } from './InteractiveFeatures'
import styles from './WelcomePage.module.css'

const TRUST_CARDS: Array<{ title: MessageKey; text: MessageKey }> = [
  { title: 'welcome.trust.card1Title', text: 'welcome.trust.card1Text' },
  { title: 'welcome.trust.card2Title', text: 'welcome.trust.card2Text' },
  { title: 'welcome.trust.card3Title', text: 'welcome.trust.card3Text' },
]

export function WelcomePage() {
  const t = useT()
  const role = useRole()

  return (
    <Screen
      top={
        // Signed in, or still loading: the home bar ("STL" with nothing on the right while it loads), never the guest bar first.
        role !== null ? (
          <TopBar home />
        ) : (
          <TopBar
            title={t('welcome.topBar')}
            titleLink="/"
            aside={
              <>
                <LanguageSwitch />
                <TextAction to={logInPath}>{t('common.logIn')}</TextAction>
              </>
            }
          />
        )
      }
    >
      <div className={styles.page}>
        {/* Hero Section */}
        <header className={styles.hero}>
          <h1 className={styles.title}>{t('welcome.hero.title')}</h1>
          <p className={styles.lead}>{t('welcome.hero.lead')}</p>
        </header>

        {/* Feature Highlights Section */}
        <section aria-labelledby="section-capabilities">
          <div className={styles.sectionHeader}>
            <span className={styles.eyebrow}>{t('welcome.features.eyebrow')}</span>
            <h2 id="section-capabilities" className={styles.sectionTitle}>
              {t('welcome.features.title')}
            </h2>
            <p className={styles.lead}>{t('welcome.features.lead')}</p>
          </div>

          <InteractiveFeatures />
        </section>

        {/* Trust & Architecture Section */}
        <section className={styles.trustSection} aria-labelledby="section-trust">
          <div className={styles.sectionHeader}>
            <span className={styles.eyebrow}>{t('welcome.trust.eyebrow')}</span>
            <h2 id="section-trust" className={styles.sectionTitle}>
              {t('welcome.trust.title')}
            </h2>
          </div>

          <div className={styles.trustGrid}>
            {TRUST_CARDS.map(({ title, text }) => (
              <div key={title} className={styles.trustCard}>
                <h3 className={styles.trustCardTitle}>{t(title)}</h3>
                <p className={styles.trustCardText}>{t(text)}</p>
              </div>
            ))}
          </div>
        </section>

        {/* Bottom CTA Banner */}
        <section className={styles.bottomCta} aria-labelledby="section-bottom-cta">
          <h2 id="section-bottom-cta" className={styles.bottomTitle}>
            {t('welcome.bottom.title')}
          </h2>
          <p className={styles.bottomLead}>{t('welcome.bottom.lead')}</p>

          <div className={styles.actionRow}>
            <Link to={pickPath} className={styles.primaryBtn}>
              {t('welcome.bottom.tryGuest')}
            </Link>
            <Link to={signUpPath} className={styles.secondaryBtn}>
              {t('welcome.bottom.register')}
            </Link>
          </div>
        </section>
      </div>
    </Screen>
  )
}
