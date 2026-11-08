import { api, type StrategyTemplate, type TemplateDetail, type TemplateKey } from '../../api'
import { useLoadable, type Loadable } from '../../lib/useLoadable'

export const useTemplates = (): Loadable<StrategyTemplate[]> => useLoadable('templates', api.getTemplates, { fixed: true })

/** The template as its active version, or as `version` (a strategy's own: versions never change, so it is cached apart). */
export const useTemplate = (key: TemplateKey | undefined, version?: number): Loadable<TemplateDetail> =>
  useLoadable(key ? (version === undefined ? `template:${key}` : `template:${key}:v${version}`) : null, () => api.getTemplate(key!, version), {
    fixed: true,
  })
