import type { Translate } from '../../../i18n/I18n'
import type { AiSource } from '../NewStrategyDraft'

/** "What the AI reads": each source on adds a section to the prompt. */
export const aiSources = (t: Translate): { id: AiSource; title: string; detail: string }[] => [
  { id: 'news', title: t('newStrategy.aiSource.news.title'), detail: t('newStrategy.aiSource.news.detail') },
  { id: 'mood', title: t('newStrategy.aiSource.mood.title'), detail: t('newStrategy.aiSource.mood.detail') },
  { id: 'figures', title: t('newStrategy.aiSource.figures.title'), detail: t('newStrategy.aiSource.figures.detail') },
]
