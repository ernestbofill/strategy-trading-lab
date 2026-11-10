/**
 * The app's own zoom (spec: Settings → Zoom): makes every screen bigger on this device, like the browser's zoom, for
 * people who want larger text. Kept on the device, not the account: a phone and a desktop often want different sizes.
 */
export const ZOOMS = [
  { value: '1', label: '100%' },
  { value: '1.1', label: '110%' },
  { value: '1.25', label: '125%' },
] as const

export type UiZoom = (typeof ZOOMS)[number]['value']

const STORAGE_KEY = 'stl.zoom'
const DEFAULT_ZOOM: UiZoom = '1'

const isZoom = (value: unknown): value is UiZoom => ZOOMS.some((z) => z.value === value)

export function storedUiZoom(): UiZoom {
  try {
    const value = localStorage.getItem(STORAGE_KEY)
    return isZoom(value) ? value : DEFAULT_ZOOM
  } catch {
    return DEFAULT_ZOOM
  }
}

let current: UiZoom = DEFAULT_ZOOM

/** The zoom factor now. Coordinates from the browser (a click, a selection) are divided by it to place an element. */
export const uiZoom = () => Number(current)

/** Zooms the whole page; `--ui-zoom` lets CSS keep `100vw` and `100dvh` to the screen (they grow with the zoom). */
export function applyUiZoom(zoom: UiZoom) {
  current = zoom
  const root = document.documentElement.style
  if (zoom === DEFAULT_ZOOM) {
    root.removeProperty('zoom')
    root.removeProperty('--ui-zoom')
  } else {
    root.setProperty('zoom', zoom)
    root.setProperty('--ui-zoom', zoom)
  }
}

/** Applies the zoom and keeps it for the next visit on this device. */
export function setUiZoom(zoom: UiZoom) {
  applyUiZoom(zoom)
  try {
    localStorage.setItem(STORAGE_KEY, zoom)
  } catch {
    // Private mode or full storage: the zoom then lasts until the page is closed.
  }
}
