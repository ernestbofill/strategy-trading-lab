import { useNavigate, useParams } from 'react-router-dom'
import type { TemplateKey } from '../../../api'
import { LoadableScreen } from '../../../components/LoadableScreen'
import { TopBar } from '../../../components/Screen'
import { PromptEditor } from '../../ai-round/PromptEditor'
import { useNewStrategyDraft } from '../NewStrategyDraft'
import { reviewPath } from '../paths'
import { settingValues } from '../settings'
import { useTemplate } from '../useTemplate'

/** Edit prompt during the new-strategy flow: the choices live in the draft until the strategy starts. */
export function DraftPromptEditor() {
  const { key } = useParams<{ key: TemplateKey }>()
  const draft = useNewStrategyDraft()
  const navigate = useNavigate()
  const top = <TopBar back={reviewPath(key ?? '')} aside={<span>PROMPT</span>} />
  return (
    <LoadableScreen state={useTemplate(key)} top={top} what="the template">
      {(template) => {
        const mine = draft.templateKey === template.key
        const stocks = template.watches.kind === 'stocks' ? template.watches.starterList : []
        const everyWeeks = Number(settingValues(template, mine ? draft.settings : {}).everyWeeks ?? template.aiRound?.everyWeeks ?? 2)
        return (
          <PromptEditor
            top={top}
            initial={mine ? { ...draft.aiSources, instructions: draft.aiInstructions } : { news: true, mood: true, figures: true, instructions: null }}
            everyWeeks={everyWeeks}
            assetIds={stocks.filter((s) => !(mine && draft.unticked.includes(s.symbol))).map((s) => s.id)}
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
