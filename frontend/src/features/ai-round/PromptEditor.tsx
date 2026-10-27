import { useState, type ReactNode } from 'react'
import type { AiSetup } from '../../api'
import { FormMessage, TextAreaField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen } from '../../components/Screen'
import { Section } from '../../components/Section'
import { ToggleTiles } from '../../components/ToggleTiles'
import { useT } from '../../i18n/I18n'
import type { AiSource } from '../new-strategy/draftState'
import { aiSources } from '../new-strategy/review/aiSources'
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
  const t = useT()
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
      setMessage({ tone: 'error', text: error instanceof Error ? error.message : t('aiRound.saveFailed') })
      setSaving(false)
    }
  }

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction onClick={() => void save()} disabled={saving || !preview}>
          {t(saving ? 'aiRound.saving' : 'aiRound.editor.save')}
        </PrimaryAction>
      }
      note={t('common.disclaimer')}
    >
      <PageHeader title={t('aiRound.editor.title')} />
      <p className={styles.plain}>{t('aiRound.editor.intro')}</p>
      <Section title={t('newStrategy.review.aiReads')}>
        <ToggleTiles
          tiles={aiSources(t)}
          isOn={(id) => sources[id]}
          onToggle={(id) => setSources((s) => ({ ...s, [id]: !s[id] }))}
          keepOne
        />
        <p className="caption">{t('aiRound.editor.sourcesNote')}</p>
      </Section>
      <Section title={t('aiRound.editor.instructions')}>
        <div className={styles.editor}>
          <TextAreaField
            label={t('aiRound.editor.instructionsLabel')}
            value={text ?? suggested}
            onChange={setText}
            rows={10}
            maxLength={MAX_INSTRUCTIONS}
            hint={t('aiRound.editor.hint')}
            hideLabel
          />
          <button type="button" className={`ui-strong ${styles.secondary}`} onClick={() => setText(null)} disabled={!edited}>
            {t('aiRound.editor.reset')}
          </button>
        </div>
      </Section>
      <Section title={t('aiRound.editor.whole')}>
        {preview ? (
          <PromptText text={preview.text} version={preview.version} edited={edited} />
        ) : (
          <p className={styles.plain}>{t(failed ? 'aiRound.prompt.loadError' : 'aiRound.prompt.loading')}</p>
        )}
      </Section>
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </Screen>
  )
}
