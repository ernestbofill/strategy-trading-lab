import { useSearchParams } from 'react-router-dom'
import { confirmPath } from './paths'

/**
 * True when the user got here from an Edit link on "Ready to start" (5 / 5).
 * Then Back and Continue return to that screen instead of the next step of the flow.
 */
export function useEditing(): boolean {
  const [params] = useSearchParams()
  return params.get('from') === 'confirm'
}

/** Where Back and Continue go: the confirm screen while editing, else the screen's usual target. */
export function useFlowTarget(usual: string): string {
  return useEditing() ? confirmPath : usual
}
