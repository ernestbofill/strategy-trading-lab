import type { Rule, Setting, TemplateDetail } from '../../api'
import { tNow } from '../../i18n/I18n'

export type SettingValues = Record<string, number>

/** The template's tested value of each of its settings. */
export function testedValues(template: TemplateDetail): SettingValues {
  return Object.fromEntries(template.settings.map((s) => [s.key, Number(template.defaults.params[s.key])]))
}

/** The tested values with the user's changes over them. */
export function settingValues(template: TemplateDetail, changed: SettingValues): SettingValues {
  return { ...testedValues(template), ...changed }
}

/** Template rules with their placeholders filled from the tested defaults and the user's settings. */
export function rulesWithSettings(template: TemplateDetail, changed: SettingValues): Rule[] {
  const params = { ...template.defaults.params, ...settingValues(template, changed) }
  return template.rules.map((rule) => ({
    ...rule,
    text: rule.text.replace(/\{(\w+)\}/g, (placeholder, key: string) =>
      Object.hasOwn(params, key) ? String(params[key]) : placeholder,
    ),
  }))
}

/** Only the values that differ from the tested ones: what the draft keeps and the API gets. */
export function changedSettings(template: TemplateDetail, values: SettingValues): SettingValues {
  const tested = testedValues(template)
  return Object.fromEntries(Object.entries(values).filter(([key, value]) => key in tested && value !== tested[key]))
}

/**
 * What a setting may be now: its range, kept a step under the setting it must stay below, and a step over any
 * setting that must stay below it (the discount zone starts before it ends).
 */
export function settingBounds(setting: Setting, settings: Setting[], values: SettingValues): { min: number; max: number } {
  let { min, max } = setting
  if (setting.below) max = Math.min(max, values[setting.below] - setting.step)
  for (const other of settings) {
    if (other.below === setting.key) min = Math.max(min, values[other.key] + setting.step)
  }
  return { min, max }
}

/** The value one step up (+1) or down (−1), rounded to the step's decimals so 1.2 + 0.1 is 1.3. */
export function stepSetting(setting: Setting, value: number, direction: 1 | -1): number {
  const decimals = String(setting.step).split('.')[1]?.length ?? 0
  return Number((value + direction * setting.step).toFixed(decimals))
}

/** "5%", "60 days". */
export const formatSetting = (setting: Setting, n: number) => setting.value.replace('{n}', String(n))

/** The Confirm and Review line: "Tested defaults" or "2 changed". */
export function settingsSummary(changed: SettingValues): string {
  const n = Object.keys(changed).length
  return n === 0 ? tNow('newStrategy.settings.tested') : tNow('newStrategy.settings.changed', { n })
}
