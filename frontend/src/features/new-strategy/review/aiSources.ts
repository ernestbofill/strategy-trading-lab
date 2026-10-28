import type { Security, TemplateDetail } from '../../../api'
import type { Translate } from '../../../i18n/I18n'
import type { AiSource } from '../NewStrategyDraft'
import { settingValues, type SettingValues } from '../settings'

/** "What the AI reads": each source on adds a section to the prompt. */
export const aiSources = (t: Translate): { id: AiSource; title: string; detail: string }[] => [
  { id: 'news', title: t('newStrategy.aiSource.news.title'), detail: t('newStrategy.aiSource.news.detail') },
  { id: 'mood', title: t('newStrategy.aiSource.mood.title'), detail: t('newStrategy.aiSource.mood.detail') },
  { id: 'figures', title: t('newStrategy.aiSource.figures.title'), detail: t('newStrategy.aiSource.figures.detail') },
]

/** What a template's prompt is written for besides the sources: the rhythm in days, and the stocks still ticked. */
export function aiPromptScope(template: TemplateDetail, settings: SettingValues, ticked: Security[]): { everyDays: number; assetIds: string[] } {
  return {
    everyDays: Number(settingValues(template, settings).everyDays ?? template.aiRound?.everyDays ?? 14),
    assetIds: ticked.map((s) => s.id),
  }
}
