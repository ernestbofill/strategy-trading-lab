/** Set once this device has logged in, so app start offers Log-in rather than Sign-up. Per device, not a secret. */
const KNOWN_KEY = 'stl.hasAccount'

export function hasLoggedInHere(): boolean {
  try {
    return localStorage.getItem(KNOWN_KEY) === '1'
  } catch {
    return false
  }
}

export function rememberLoggedIn() {
  try {
    localStorage.setItem(KNOWN_KEY, '1')
  } catch {
    // Private windows may refuse storage; app start then offers Sign-up, with a link to Log-in.
  }
}
