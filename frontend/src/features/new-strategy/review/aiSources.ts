import type { AiSource } from '../NewStrategyDraft'

/** "What the AI reads": each source on adds a section to the prompt. */
export const AI_SOURCES: { id: AiSource; title: string; detail: string }[] = [
  { id: 'news', title: 'News', detail: 'Recent stories' },
  { id: 'mood', title: 'Mood', detail: 'How people talk about it' },
  { id: 'figures', title: 'Figures', detail: 'Quarterly numbers' },
]
