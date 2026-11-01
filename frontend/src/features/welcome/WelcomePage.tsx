import { Link } from 'react-router-dom'
import { Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { logInPath, signUpPath } from '../account/paths'
import { pickPath } from '../new-strategy/paths'
import { InteractiveFeatures } from './InteractiveFeatures'
import styles from './WelcomePage.module.css'

export function WelcomePage() {
  const t = useT()

  return (
    <Screen
      top={
        <TopBar
          title={t('welcome.topBar')}
          titleLink="/"
          aside={<TextAction to={logInPath}>{t('welcome.logIn')}</TextAction>}
        />
      }
    >
      <div className={styles.page}>
        {/* Hero Section */}
        <header className={styles.hero}>
          <span className={styles.eyebrow}>{t('welcome.hero.eyebrow')}</span>
          <h1 className={styles.title}>{t('welcome.hero.title')}</h1>
          <p className={styles.lead}>{t('welcome.hero.lead')}</p>

          <div className={styles.actionRow}>
            <Link to={pickPath} className={styles.primaryBtn}>
              {t('welcome.hero.tryGuest')}
            </Link>
            <Link to={signUpPath} className={styles.secondaryBtn}>
              {t('welcome.hero.register')}
            </Link>
          </div>

          <p className={styles.haveAccountLink}>
            <Link to={logInPath}>{t('welcome.hero.haveAccount')}</Link>
          </p>
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
            <div className={styles.trustCard}>
              <h3 className={styles.trustCardTitle}>{t('welcome.trust.card1Title')}</h3>
              <p className={styles.trustCardText}>{t('welcome.trust.card1Text')}</p>
            </div>
            <div className={styles.trustCard}>
              <h3 className={styles.trustCardTitle}>{t('welcome.trust.card2Title')}</h3>
              <p className={styles.trustCardText}>{t('welcome.trust.card2Text')}</p>
            </div>
            <div className={styles.trustCard}>
              <h3 className={styles.trustCardTitle}>{t('welcome.trust.card3Title')}</h3>
              <p className={styles.trustCardText}>{t('welcome.trust.card3Text')}</p>
            </div>
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

        {/* Disclaimer */}
        <div className={styles.disclaimerBox}>
          <p className="caption">{t('common.disclaimerFull')}</p>
        </div>
      </div>
    </Screen>
  )
}
