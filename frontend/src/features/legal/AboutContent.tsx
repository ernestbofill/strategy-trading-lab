import { Section } from '../../components/Section'
import styles from './LegalDoc.module.css'

export function AboutContent() {
  return (
    <div className={styles.prose}>
      <Section title="1. About Strategy Trading Lab">
        <p>
          Strategy Trading Lab was created as an independent, transparent workbench for disciplined rule-based investing. Most private investors struggle with market volatility because emotional bias, fear, and greed override rational decision-making. Strategy Trading Lab replaces guesswork with transparent mathematical rules, clear candidate criteria, and rigorous historical backtesting.
        </p>
      </Section>

      <Section title="2. Open methodology">
        <p>
          We do not believe in black boxes, proprietary secrets, or exaggerated return promises. Every template offered in Strategy Trading Lab explains its underlying indicators (such as SMA 200, 52-week highs, or drawdown zones) in plain language, allowing you to tune parameters, test assumptions, and understand every calculation before following it.
        </p>
      </Section>

      <Section title="3. Conflicts of interest disclosure (EU MAR)">
        <p>
          In the interest of full transparency and consistent with European Market Abuse Regulation (MAR) design standards:
        </p>
        <ul className={styles.list}>
          <li>
            <strong>Personal holdings:</strong> The platform creator and operator, Ernest Bofill, personally invests in broad market index funds (such as S&amp;P 500 ETFs / SPY) and may occasionally hold shares in individual companies monitored by platform templates.
          </li>
          <li>
            <strong>Zero broker kickbacks or affiliate fees:</strong> Strategy Trading Lab receives no compensation, referral commissions, rebates, or payment for order flow from brokers or market makers.
          </li>
          <li>
            <strong>No front-running:</strong> Operating policies strictly prohibit trading ahead of user signals or taking financial advantage of user activity or strategy parameters.
          </li>
        </ul>
      </Section>
    </div>
  )
}
