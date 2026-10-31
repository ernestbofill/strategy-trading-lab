import { Section } from '../../components/Section'
import { useT } from '../../i18n/I18n'
import { TERMS_VERSION } from '../account/SignUp'
import styles from './LegalDoc.module.css'

export function TermsOfUseContent() {
  const t = useT()

  return (
    <div className={styles.prose}>
      <div className={styles.meta}>Version {TERMS_VERSION} · Effective October 31, 2026</div>

      <div className={styles.callout}>
        <strong>Core principle:</strong> {t('common.disclaimerFull')} {t('common.pastResults')}
      </div>

      <Section title="1. Purpose of the platform">
        <p>
          Strategy Trading Lab is a personal strategy lab and rule monitoring platform designed for quantitative research,
          strategy testing, and disciplined trade tracking.
        </p>
      </Section>

      <Section title="2. What the service never does">
        <p>Strategy Trading Lab is strictly a software tool. It operates under clear technical boundaries:</p>
        <ul className={styles.list}>
          <li>
            <strong>Never provides financial advice:</strong> The platform does not give investment advice, financial planning,
            or personalized recommendations.
          </li>
          <li>
            <strong>Never connects to a broker:</strong> The platform does not connect to, integrate with, or read data from any
            brokerage account.
          </li>
          <li>
            <strong>Never places trades or orders:</strong> The application does not execute trades, place orders, or hold client funds.
          </li>
          <li>
            <strong>Never assesses suitability:</strong> The platform never evaluates your financial circumstances, income, net
            worth, or risk tolerance.
          </li>
        </ul>
      </Section>

      <Section title="3. Two operational modes">
        <p>The app offers two distinct, unmixed operating modes:</p>
        <ul className={styles.list}>
          <li>
            <strong>Simulation mode:</strong> The app records paper trades when mathematical strategy conditions are met, tracking
            hypothetical performance without real capital.
          </li>
          <li>
            <strong>Live mode:</strong> The app sends notification alerts when your configured rules trigger. You independently evaluate
            each signal, decide whether to trade at your own external broker, and confirm your actions manually in the app.
          </li>
        </ul>
      </Section>

      <Section title="4. Past performance and backtests">
        <p>
          All backtest results and historical returns reflect rule simulations calculated against historical price data. Past
          results don't promise future ones. Market conditions change continuously, and historical results do not guarantee future
          outcomes.
        </p>
      </Section>

      <Section title="5. Account responsibilities and access">
        <p>
          You are responsible for keeping your account credentials confidential. You agree not to abuse server resources, exceed
          rate limits, or reverse-engineer private endpoints. Strategy Trading Lab is currently a free research and hobby project
          provided &ldquo;as is&rdquo; without warranties of any kind.
        </p>
      </Section>

      <Section title="6. Governing law">
        <p>
          These terms are governed by the laws of Spain and applicable European Union regulations. Any dispute arising from or related to the use of Strategy Trading Lab shall be submitted to the competent courts of Barcelona, Spain.
        </p>
      </Section>
    </div>
  )
}
