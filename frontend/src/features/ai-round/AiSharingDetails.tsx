import styles from './AiRound.module.css'

/** "What we keep": what an anonymously shared AI round holds, and what it never holds. */
export function AiSharingDetails() {
  return (
    <details className={styles.details}>
      <summary className="ui-strong">What we keep</summary>
      <p className="caption">
        For each shared round: the AI model, the day, the prompt's version and whether you edited it, the sources on, the
        stocks' symbols, your AI's answer and its scores. Before it is stored, anything that looks like an email address, a
        phone number or a link's parameters is removed.
      </p>
      <p className="caption">
        Never with it: your name, email, account, strategy name, budget, steps, trades, or whether you acted. Nothing links it
        back to you, so a shared round can't be removed later. Turning sharing off stops it for the next rounds.
      </p>
    </details>
  )
}
