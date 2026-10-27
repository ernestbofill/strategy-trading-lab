import { useEffect, useState } from 'react'
import { api, type AiPromptPreview, type AiPromptRequest } from '../../api'

/** The prompt for these choices, from the API (the one place that writes it), asked again a moment after they change. */
export function useAiPromptPreview(request: AiPromptRequest): { preview: AiPromptPreview | null; failed: boolean } {
  const [state, setState] = useState<{ preview: AiPromptPreview | null; failed: boolean }>({ preview: null, failed: false })
  const key = JSON.stringify(request)
  useEffect(() => {
    let current = true
    const timer = setTimeout(() => {
      api.previewAiPrompt(JSON.parse(key) as AiPromptRequest).then(
        (preview) => current && setState({ preview, failed: false }),
        () => current && setState((s) => ({ ...s, failed: true })),
      )
    }, 250)
    return () => {
      current = false
      clearTimeout(timer)
    }
  }, [key])
  return state
}
