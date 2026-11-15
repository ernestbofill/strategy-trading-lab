import { api, type StrategyTemplate, type TemplateDetail, type TemplateKey } from '../../api'
import { useLoadable, type Loadable } from '../../lib/useLoadable'

export const templatesCacheKey = 'templates'
/** The template as its active version; a strategy's own version is cached apart (`:v2`). */
export const templateCacheKey = (key: string) => `template:${key}`

export const useTemplates = (): Loadable<StrategyTemplate[]> => useLoadable(templatesCacheKey, api.getTemplates, { fixed: true })

/** The template as its active version, or as `version` (a strategy's own: versions never change, so it is cached apart). */
export const useTemplate = (key: TemplateKey | undefined, version?: number): Loadable<TemplateDetail> =>
  useLoadable(key ? (version === undefined ? templateCacheKey(key) : `${templateCacheKey(key)}:v${version}`) : null, () => api.getTemplate(key!, version), {
    fixed: true,
  })
