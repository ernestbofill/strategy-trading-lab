import { confirmPath, reviewPath } from './paths'

/**
 * Where the user came from when they open a screen to change something.
 * - `confirm`: an Edit link on "Ready to start" (5 / 5). Back and Continue return there.
 * - `review`: a sub-page (How we tested, Advanced settings) opened from a review that is itself being edited.
 *   Back returns to that review, which then returns to the confirm screen.
 */
export type EditSource = 'confirm' | 'review'

const PARAM = 'from'
const SOURCES: readonly EditSource[] = ['confirm', 'review']

/** `path` marked as opened from `source`. */
export const withEditSource = (path: string, source: EditSource) => `${path}?${PARAM}=${source}`

/** Link target for an Edit link on the confirm screen. */
export const editFromConfirm = (path: string) => withEditSource(path, 'confirm')

/**
 * The edit source in a URL's query string. Ignored until the user has reached the confirm screen, so a
 * stale or typed link cannot skip the steps of the flow.
 */
export function readEditSource(search: string, reachedConfirm: boolean): EditSource | null {
  if (!reachedConfirm) return null
  const value = new URLSearchParams(search).get(PARAM)
  return SOURCES.find((s) => s === value) ?? null
}

/** Where Continue and Back go: the confirm screen while editing from it, else the screen's usual target. */
export const flowTarget = (source: EditSource | null, usual: string) => (source === 'confirm' ? confirmPath : usual)

/** Link from a review to one of its sub-pages: keeps the edit going when the review is being edited. */
export const subPageLink = (source: EditSource | null, path: string) =>
  source === 'confirm' ? withEditSource(path, 'review') : path

/** Back from a review's sub-page. */
export function reviewSubPageBack(source: EditSource | null, templateKey: string): string {
  if (source === 'confirm') return confirmPath
  return source === 'review' ? editFromConfirm(reviewPath(templateKey)) : reviewPath(templateKey)
}
