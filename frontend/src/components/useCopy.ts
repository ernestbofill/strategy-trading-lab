import { useEffect, useState } from 'react'

/**
 * Copies text to the clipboard. `copied` says how the last copy went for a moment (true; false when the browser refused), then
 * goes back to null; `copy` resolves to whether it worked, for a caller that says more on a failure.
 */
export function useCopy(showFor = 2000) {
  const [copied, setCopied] = useState<boolean | null>(null)

  useEffect(() => {
    if (copied === null) return
    const id = window.setTimeout(() => setCopied(null), showFor)
    return () => window.clearTimeout(id)
  }, [copied, showFor])

  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
      return true
    } catch {
      setCopied(false)
      return false
    }
  }

  return { copied, copy }
}
