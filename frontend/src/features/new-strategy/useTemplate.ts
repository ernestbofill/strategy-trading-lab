import { api, type StrategyTemplate, type TemplateDetail, type TemplateKey } from '../../api'
import { useLoadable, type Loadable } from '../../lib/useLoadable'

export const useTemplates = (): Loadable<StrategyTemplate[]> => useLoadable('templates', api.getTemplates, { fixed: true })

export const useTemplate = (key: TemplateKey | undefined): Loadable<TemplateDetail> =>
  useLoadable(key ? `template:${key}` : null, () => api.getTemplate(key!), { fixed: true })
