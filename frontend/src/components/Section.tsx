import type { ReactNode } from 'react'
import styles from './Section.module.css'

/** A titled block of a screen. `aside` sits right of the heading (a counter, an Edit link); `id` lets a link open the screen at it. */
export function Section({ id, title, aside, children }: { id?: string; title: string; aside?: ReactNode; children: ReactNode }) {
  return (
    <section id={id} className={styles.section}>
      <div className={styles.head}>
        <h2 className={`ui-strong ${styles.title}`}>{title}</h2>
        {aside}
      </div>
      {children}
    </section>
  )
}
