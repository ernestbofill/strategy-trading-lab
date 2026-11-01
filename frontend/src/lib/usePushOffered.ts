import { useEffect, useState } from 'react'
import { pushKey, pushSupport } from './push'

/**
 * Whether the app offers push here: the server sends it and this browser can get it (on iPhone, once on the home screen).
 * False until known, so the choice never flashes in and out.
 */
export function usePushOffered(): boolean {
  const [offered, setOffered] = useState(false)
  useEffect(() => {
    if (pushSupport() === 'unsupported') return
    let live = true
    pushKey().then(
      (key) => live && setOffered(key !== null),
      () => {}, // No key, no push: email still works.
    )
    return () => {
      live = false
    }
  }, [])
  return offered
}
