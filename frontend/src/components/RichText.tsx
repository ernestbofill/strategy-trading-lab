import { Fragment } from 'react'
import styles from './RichText.module.css'

/** Renders **label** in bold primary and `term` in highlighted mono. Nothing else is parsed. */
export function RichText({ text }: { text: string }) {
  const parts = text.split(/(\*\*[^*]+\*\*|`[^`]+`)/g)
  return (
    <>
      {parts.map((part, i) => {
        if (part.startsWith('**')) return <b key={i} className={styles.label}>{part.slice(2, -2)}</b>
        if (part.startsWith('`')) return <span key={i} className={styles.term}>{part.slice(1, -1)}</span>
        return <Fragment key={i}>{part}</Fragment>
      })}
    </>
  )
}
