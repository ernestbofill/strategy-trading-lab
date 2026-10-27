import { useNavigate, useParams } from 'react-router-dom'
import type { TemplateKey } from '../../../api'
import { LoadableScreen } from '../../../components/LoadableScreen'
import { TopBar } from '../../../components/Screen'
import { useT } from '../../../i18n/I18n'
import { PromptEditor } from '../../ai-round/PromptEditor'
import { useNewStrategyDraft } from '../NewStrategyDraft'
import { reviewPath } from '../paths'
import { useTemplate } from '../useTemplate'
import { aiPromptScope } from './aiSources'

/** Edit prompt during the new-strategy flow: the choices live in the draft until the strategy starts. */
export function DraftPromptEditor() {
  const { key } = useParams<{ key: TemplateKey }>()
  const draft = useNewStrategyDraft()
  const t = useT()
  const navigate = useNavigate()
  const top = <TopBar back={reviewPath(key ?? '')} aside={<span>{t('aiRound.editor.topBar')}</span>} />
  return (
    <LoadableScreen state={useTemplate(key)} top={top} what={t('newStrategy.what.template')}>
      {(template) => {
        const mine = draft.templateKey === template.key
        const { everyDays, assetIds } = aiPromptScope(template, mine ? draft.settings : {}, mine ? draft.unticked : [])
        return (
          <PromptEditor
            top={top}
            initial={mine ? { ...draft.aiSources, instructions: draft.aiInstructions } : { news: true, mood: true, figures: true, instructions: null }}
            everyDays={everyDays}
            assetIds={assetIds}
            onSave={async ({ instructions, ...sources }) => {
              if (!mine) draft.switchTemplate(template.key)
              draft.update({ aiSources: sources, aiInstructions: instructions })
              navigate(reviewPath(template.key))
            }}
          />
        )
      }}
    </LoadableScreen>
  )
}
