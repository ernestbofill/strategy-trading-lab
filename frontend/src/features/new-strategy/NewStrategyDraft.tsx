import { createContext, useContext, useState, type ReactNode } from 'react'
import type { TemplateKey } from '../../api'

// Choices made across the new-strategy flow (1 / 5 … 5 / 5), kept while the user moves back and forth.
interface Draft {
  templateKey: TemplateKey
  setTemplateKey: (key: TemplateKey) => void
}

const DraftContext = createContext<Draft | null>(null)

export function NewStrategyDraftProvider({ children }: { children: ReactNode }) {
  // Dip in winners is selected by default, so "Continue" is always enabled.
  const [templateKey, setTemplateKey] = useState<TemplateKey>('dip')
  return <DraftContext.Provider value={{ templateKey, setTemplateKey }}>{children}</DraftContext.Provider>
}

export function useNewStrategyDraft(): Draft {
  const draft = useContext(DraftContext)
  if (!draft) throw new Error('useNewStrategyDraft must be used inside NewStrategyDraftProvider')
  return draft
}
