import { Section } from '../../components/Section'
import { useT } from '../../i18n/I18n'
import styles from './LegalDoc.module.css'

export function DisclaimerContent() {
  const t = useT()

  return (
    <div className={styles.prose}>
      <div className={styles.callout}>
        <strong>Full statutory disclaimer:</strong> {t('common.disclaimerFull')} {t('common.pastResults')}
      </div>

      <Section title="1. Not financial advice">
        <p>
          Strategy Trading Lab is an educational and technical analysis tool. The content, metrics, calculations, backtests, and signals generated or displayed in the platform do not constitute investment advice, financial analysis, investment recommendations, or an offer or solicitation to buy or sell any financial instrument.
        </p>
      </Section>

      <Section title="2. Risk of capital loss">
        <p>
          Investing and trading in stocks, exchange-traded funds (ETFs), and other securities carries substantial financial risk. Market prices fluctuate unpredictable and rapidly. You may lose some or all of the money you invest. Never invest funds you cannot afford to lose completely.
        </p>
      </Section>

      <Section title="3. Algorithmic transparency and limitations">
        <p>
          Signals and indicators are automated mathematical evaluations based on historical end-of-day market data provided by external sources (including Yahoo Finance). Data feeds may suffer delays, inaccuracies, or unexpected corporate action adjustments. The platform does not guarantee continuous availability, error-free calculations, or specific market returns.
        </p>
      </Section>

      <Section title="4. Independent execution at external brokers">
        <p>
          Strategy Trading Lab never connects to your broker, never receives order routing permissions, and never executes trades on your behalf. Any transaction you enter into is executed solely by you at your own independent brokerage institution, subject to your own evaluation and risk assessment.
        </p>
      </Section>
    </div>
  )
}
