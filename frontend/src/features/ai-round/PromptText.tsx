import { useState } from 'react'
import { useT } from '../../i18n/I18n'
import styles from './AiRound.module.css'

interface PromptTextProps {
  text: string
  version: number
  edited: boolean
  /** Show only the first lines, with a button for the rest (the AI round's preview). */
  collapsed?: boolean
}

/** The prompt shown as code, read-only, under a comment that says whose it is. */
export function PromptText({ text, version, edited, collapsed }: PromptTextProps) {
  const t = useT()
  const [open, setOpen] = useState(!collapsed)
  const lines = text.split('\n')
  const shown = open ? lines : lines.slice(0, 3)
  return (
    <div className={`well ${styles.prompt}`} aria-label={t('aiRound.prompt.aria')}>
      <p className={styles.promptComment}>{t(edited ? 'aiRound.prompt.yours' : 'aiRound.prompt.suggested', { version })}</p>
      {shown.map((line, i) => (
        <p key={i} className={line.startsWith('|') ? styles.promptTable : line.startsWith('- ') ? styles.promptList : undefined}>
          {line || ' '}
        </p>
      ))}
      {collapsed && (
        <button type="button" className={`ui-strong ${styles.more}`} aria-expanded={open} onClick={() => setOpen(!open)}>
          {t(open ? 'aiRound.prompt.less' : 'aiRound.prompt.more')}
        </button>
      )}
    </div>
  )
}
