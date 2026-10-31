import { Section } from '../../components/Section'
import styles from './LegalDoc.module.css'

export function PrivacyPolicyContent() {
  return (
    <div className={styles.prose}>
      <div className={styles.meta}>Effective October 31, 2026 · General Data Protection Regulation (GDPR) Compliant</div>

      <Section title="1. Data controller">
        <p>
          The data controller responsible for the processing of personal data on Strategy Trading Lab is <strong>Ernest Bofill</strong>, residing in <strong>Barcelona, Spain</strong>.
        </p>
        <p>
          Contact email:{' '}
          <a href="mailto:contact@strategytradinglab.local" className="link">
            contact@strategytradinglab.local
          </a>
        </p>
      </Section>

      <Section title="2. Personal data collected and purpose">
        <p>We collect and process only the minimal personal data required to provide the service:</p>
        <ul className={styles.list}>
          <li>
            <strong>Account information:</strong> Email address, optional first and last name, cryptographically hashed password (PBKDF2 with HMAC-SHA512), preferred language, time zone, and display currency.
          </li>
          <li>
            <strong>Strategy and trade data:</strong> Strategies configured, watched assets, paper simulation trades, and manual trade logs entered by you.
          </li>
          <li>
            <strong>Guests exploring without an account:</strong> Draft strategies and preferences configured by guest visitors are stored solely within your local browser storage and are never transmitted to our servers until you create an account.
          </li>
        </ul>
      </Section>

      <Section title="3. Legal basis for processing">
        <p>Processing is carried out under the following legal bases pursuant to GDPR Article 6(1):</p>
        <ul className={styles.list}>
          <li>
            <strong>Performance of a contract (Art. 6(1)(b)):</strong> To manage your account, store your strategy configurations, calculate rules, and deliver email alerts you request.
          </li>
          <li>
            <strong>Legitimate interests (Art. 6(1)(f)):</strong> To maintain application security, prevent brute-force attacks and abuse through rate limiting, and diagnose system errors.
          </li>
        </ul>
      </Section>

      <Section title="4. Data processors and storage locations">
        <p>All data processors are bound by GDPR data processing agreements and operate within the European Union:</p>
        <ul className={styles.list}>
          <li>
            <strong>Oracle Cloud Infrastructure (OCI):</strong> Application server hosting in Madrid, Spain (EU).
          </li>
          <li>
            <strong>Neon, Inc.:</strong> PostgreSQL database hosting in AWS eu-central-1 region (Frankfurt, Germany, EU).
          </li>
          <li>
            <strong>Brevo (Sendinblue SAS):</strong> Transactional email delivery in France (EU).
          </li>
        </ul>
      </Section>

      <Section title="5. Cookies and tracking">
        <p>
          Strategy Trading Lab uses <strong>strictly necessary session cookies only</strong> (<code className="tag">stl_session</code>) to maintain authenticated sessions securely via <code className="tag">HttpOnly</code>, <code className="tag">Secure</code>, and <code className="tag">SameSite=Lax</code> flags.
        </p>
        <p>
          We do not use advertising cookies, profiling cookies, third-party analytics trackers, or commercial pixels. In accordance with EU Directive 2002/58/EC (ePrivacy Directive) and Spanish LSSI-CE Art. 22.2, technical session cookies do not require a cookie banner or user consent.
        </p>
      </Section>

      <Section title="6. Your rights and data retention">
        <p>
          Under GDPR Articles 15 to 22, you hold the right to access, rectify, export, and delete your personal data. You may download all account and strategy data in standard JSON format, or permanently delete your account and all associated data at any time from your Account settings.
        </p>
      </Section>
    </div>
  )
}
