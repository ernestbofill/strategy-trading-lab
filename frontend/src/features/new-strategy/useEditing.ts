import { useLocation } from 'react-router-dom'
import { flowTarget, readEditSource, reviewSubPageBack, type EditSource } from './editFlow'
import { useNewStrategyDraft } from './NewStrategyDraft'

/** Where the user came from when they opened this screen to change something, if they did. */
export function useEditSource(): EditSource | null {
  const { search } = useLocation()
  const { reachedConfirm } = useNewStrategyDraft()
  return readEditSource(search, reachedConfirm)
}

/** Where Back and Continue go: the confirm screen while editing from it, else the screen's usual target. */
export const useFlowTarget = (usual: string) => flowTarget(useEditSource(), usual)

/** Back from a review's sub-page. */
export const useReviewSubPageBack = (templateKey: string | undefined) => {
  const source = useEditSource()
  return templateKey ? reviewSubPageBack(source, templateKey) : '/'
}
