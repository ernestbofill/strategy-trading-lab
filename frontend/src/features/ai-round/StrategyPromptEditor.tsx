import { useNavigate, useParams } from 'react-router-dom'
import { api, InvalidFieldsError } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { TopBar } from '../../components/Screen'
import { useT } from '../../i18n/I18n'
import { invalidate } from '../../lib/useLoadable'
import { aiRoundPath } from '../dashboard/paths'
import { aiRoundCacheKey, useAiRound } from './useAiRound'
import { PromptEditor } from './PromptEditor'

/** Edit prompt for a running AI strategy: from the next round on, the prompt asks for what is saved here. */
export function StrategyPromptEditor() {
  const { id = '' } = useParams()
  const t = useT()
  const navigate = useNavigate()
  const round = useAiRound(id)
  const top = <TopBar back={aiRoundPath(id)} aside={<span>{t('aiRound.editor.topBar')}</span>} />
  return (
    <LoadableScreen state={round} top={top} what={t('aiRound.promptWhat')}>
      {(r) => (
        <PromptEditor
          top={top}
          initial={r.setup}
          everyDays={r.everyDays}
          assetIds={r.stocks.map((s) => s.assetId)}
          onSave={async (setup) => {
            try {
              await api.updateAiPrompt(id, setup)
            } catch (error) {
              throw new Error(error instanceof InvalidFieldsError ? error.message : t('aiRound.saveFailed'))
            }
            invalidate(aiRoundCacheKey(id))
            navigate(aiRoundPath(id))
          }}
        />
      )}
    </LoadableScreen>
  )
}
