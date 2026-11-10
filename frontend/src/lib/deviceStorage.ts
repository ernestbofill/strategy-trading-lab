/** A choice kept on this device (language, zoom). Reads fall back when storage is off or holds something unknown. */
export function readDeviceChoice<T extends string>(key: string, isValid: (value: unknown) => value is T, fallback: T): T {
  try {
    const value = localStorage.getItem(key)
    return isValid(value) ? value : fallback
  } catch {
    return fallback
  }
}

export function storeDeviceChoice(key: string, value: string) {
  try {
    localStorage.setItem(key, value)
  } catch {
    // Private mode or full storage: the choice then lasts until the page is closed.
  }
}
