import { useEffect, useState } from 'react'
import { pushKey, pushSupport } from '../../lib/push'
import { useOptionalAccount } from './AccountContext'

/**
 * Whether the app offers push here: the server sends it and this browser can get it (on iPhone, once on the home screen).
 * False until known, so the choice never flashes in and out, and for a guest: the key needs an account (a guest asking would
 * end a session they don't have), and push is chosen once signed in.
 */
export function usePushOffered(): boolean {
  const signedIn = useOptionalAccount() !== null
  const [offered, setOffered] = useState(false)
  useEffect(() => {
    if (!signedIn || pushSupport() === 'unsupported') return
    let live = true
    pushKey().then(
      (key) => live && setOffered(key !== null),
      () => {}, // No key, no push: email still works.
    )
    return () => {
      live = false
    }
  }, [signedIn])
  return signedIn && offered
}
