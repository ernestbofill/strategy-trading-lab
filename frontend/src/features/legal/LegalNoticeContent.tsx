import { Section } from '../../components/Section'
import styles from './LegalDoc.module.css'

export function LegalNoticeContent() {
  return (
    <div className={styles.prose}>
      <div className={styles.meta}>Information pursuant to Spanish e-commerce &amp; information society law (LSSI-CE Art. 10)</div>

      <Section title="1. Service operator">
        <p>
          Strategy Trading Lab is operated by <strong>Ernest Bofill</strong> as an individual, non-commercial research and hobby project based in <strong>Barcelona, Spain</strong>.
        </p>
        <p>
          Contact email:{' '}
          <a href="mailto:contact@strategytradinglab.local" className="link">
            contact@strategytradinglab.local
          </a>
        </p>
      </Section>

      <Section title="2. Purpose and legal nature">
        <p>
          Strategy Trading Lab is an educational and quantitative research software application for creating, backtesting, and monitoring rule-based trading strategies. The service is provided free of charge for non-commercial personal evaluation.
        </p>
        <p>
          The operator is not a registered investment firm, broker-dealer, financial advisor, or credit institution, and does not conduct regulated financial activities under the Spanish Securities Market Act (Ley de los Mercados de Valores) or EU Directive 2014/65/EU (MiFID II).
        </p>
      </Section>

      <Section title="3. Hosting and technical infrastructure">
        <p>The application is hosted on third-party cloud infrastructure situated in the European Union:</p>
        <ul className={styles.list}>
          <li>
            <strong>Application server:</strong> Oracle Cloud Infrastructure (OCI), Madrid Region (Spain, European Union).
          </li>
          <li>
            <strong>Database:</strong> Neon, Inc., serverless PostgreSQL, AWS eu-central-1 (Frankfurt, Germany, European Union).
          </li>
          <li>
            <strong>Email delivery:</strong> Brevo (Sendinblue SAS), Paris (France, European Union).
          </li>
        </ul>
      </Section>

      <Section title="4. Intellectual property">
        <p>
          All source code, design elements, texts, diagrams, algorithms, and documentation of Strategy Trading Lab are the intellectual property of Ernest Bofill and Strategy Trading Lab, protected by Spanish and international copyright laws.
        </p>
      </Section>
    </div>
  )
}
