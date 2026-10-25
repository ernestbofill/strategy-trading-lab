import { useNavigate, useParams } from 'react-router-dom'
import { api, InvalidFieldsError } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { TopBar } from '../../components/Screen'
import { invalidate } from '../../lib/useLoadable'
import { aiRoundPath } from '../dashboard/paths'
import { aiRoundCacheKey, useAiRound } from './useAiRound'
import { PromptEditor } from './PromptEditor'

/** Edit prompt for a running AI strategy: from the next round on, the prompt asks for what is saved here. */
export function StrategyPromptEditor() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const round = useAiRound(id)
  const top = <TopBar back={aiRoundPath(id)} aside={<span>PROMPT</span>} />
  return (
    <LoadableScreen state={round} top={top} what="this strategy's prompt">
      {(r) => (
        <PromptEditor
          top={top}
          initial={r.setup}
          everyWeeks={r.everyWeeks}
          assetIds={r.stocks.map((s) => s.assetId)}
          onSave={async (setup) => {
            try {
              await api.updateAiPrompt(id, setup)
            } catch (error) {
              throw new Error(error instanceof InvalidFieldsError ? error.message : "We couldn't save it. Try again.")
            }
            invalidate(aiRoundCacheKey(id))
            navigate(aiRoundPath(id))
          }}
        />
      )}
    </LoadableScreen>
  )
}
