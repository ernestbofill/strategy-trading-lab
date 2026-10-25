import { useState, type ReactNode } from 'react'
import type { AiSetup } from '../../api'
import { FormMessage, TextAreaField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen } from '../../components/Screen'
import { Section } from '../../components/Section'
import { ToggleTiles } from '../../components/ToggleTiles'
import { DISCLAIMER } from '../../lib/copy'
import type { AiSource } from '../new-strategy/draftState'
import { AI_SOURCES } from '../new-strategy/review/aiSources'
import styles from './AiRound.module.css'
import { PromptText } from './PromptText'
import { useAiPromptPreview } from './useAiPromptPreview'

/** The longest instructions the API keeps. */
export const MAX_INSTRUCTIONS = 4000

interface PromptEditorProps {
  top: ReactNode
  initial: AiSetup
  everyWeeks: number
  /** The stocks the prompt lists. */
  assetIds: string[]
  /** Resolves when saved; rejects with a message for the user. */
  onSave: (setup: AiSetup) => Promise<void>
}

/**
 * Edit prompt (spec: Review, "The prompt"; not drawn): what the AI reads, and the instructions in the user's own words, with
 * "Reset to suggested". The date line, the answer table and the stocks stay the app's, so it can read the answer.
 */
export function PromptEditor({ top, initial, everyWeeks, assetIds, onSave }: PromptEditorProps) {
  const [sources, setSources] = useState<Record<AiSource, boolean>>({ news: initial.news, mood: initial.mood, figures: initial.figures })
  // Null while the instructions follow the suggested ones (they change with the sources).
  const [text, setText] = useState<string | null>(initial.instructions)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<FormMessageState>(null)
  const { preview, failed } = useAiPromptPreview({ ...sources, instructions: text, everyWeeks, assetIds })
  const suggested = preview?.suggested ?? ''
  const edited = text !== null && text.trim() !== suggested.trim()

  const save = async () => {
    setSaving(true)
    setMessage(null)
    try {
      await onSave({ ...sources, instructions: edited ? text : null })
    } catch (error) {
      setMessage({ tone: 'error', text: error instanceof Error ? error.message : "We couldn't save it. Try again." })
      setSaving(false)
    }
  }

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction onClick={() => void save()} disabled={saving || !preview}>
          {saving ? 'Saving…' : 'Save prompt'}
        </PrimaryAction>
      }
      note={DISCLAIMER}
    >
      <PageHeader title="Edit prompt" />
      <p className={styles.plain}>
        Say what your AI should look at, in your words. The app adds today's date before them, and the answer table and your
        stocks after them, so it can read the answer.
      </p>
      <Section title="What the AI reads">
        <ToggleTiles
          tiles={AI_SOURCES}
          isOn={(id) => sources[id]}
          onToggle={(id) => setSources((s) => ({ ...s, [id]: !s[id] }))}
          keepOne
        />
        <p className="caption">At least one stays on. Each one adds a column to the answer table.</p>
      </Section>
      <Section title="Your instructions">
        <div className={styles.editor}>
          <TextAreaField
            label="Instructions"
            value={text ?? suggested}
            onChange={setText}
            rows={10}
            maxLength={MAX_INSTRUCTIONS}
            hint="Ask for scores against criteria, never which stock to buy."
            hideLabel
          />
          <button type="button" className={`ui-strong ${styles.secondary}`} onClick={() => setText(null)} disabled={!edited}>
            ⟳ Reset to suggested
          </button>
        </div>
      </Section>
      <Section title="The whole prompt">
        {preview ? (
          <PromptText text={preview.text} version={preview.version} edited={edited} />
        ) : (
          <p className={styles.plain}>{failed ? "We couldn't load the prompt. Try again later." : 'Loading the prompt…'}</p>
        )}
      </Section>
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </Screen>
  )
}
