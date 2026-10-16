import styles from './Tagline.module.css'

/** A template's tagline, always in quotes. */
export function Tagline({ text }: { text: string }) {
  return <span className={styles.tagline}>"{text}"</span>
}
