import { tNow } from '../../../i18n/I18n'
import type { AiSource } from '../NewStrategyDraft'

/** "What the AI reads": each source on adds a section to the prompt. */
export const AI_SOURCES: { id: AiSource; title: string; detail: string }[] = [
  { id: 'news', title: tNow('newStrategy.aiSource.news.title'), detail: tNow('newStrategy.aiSource.news.detail') },
  { id: 'mood', title: tNow('newStrategy.aiSource.mood.title'), detail: tNow('newStrategy.aiSource.mood.detail') },
  { id: 'figures', title: tNow('newStrategy.aiSource.figures.title'), detail: tNow('newStrategy.aiSource.figures.detail') },
]
