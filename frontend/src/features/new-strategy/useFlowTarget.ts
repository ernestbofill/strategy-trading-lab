import { useNewStrategyDraft } from './NewStrategyDraft'
import { confirmPath } from './paths'

/** Where Continue goes: the confirm screen while the user changes something from it, else `usual`. */
export const useFlowTarget = (usual: string) => (useNewStrategyDraft().editing ? confirmPath : usual)
