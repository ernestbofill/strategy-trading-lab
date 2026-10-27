import { ForbiddenError, InvalidValueError, TooManyTriesError, type FeedbackItem, type FeedbackKind } from '../../api'
import type { Translate } from '../../i18n/I18n'

export const FEEDBACK_KINDS: { value: FeedbackKind; label: string; emoji: string }[] = [
  { value: 'bug', label: 'Bug', emoji: '🐞' },
  { value: 'idea', label: 'Idea', emoji: '💡' },
  { value: 'note', label: 'Note', emoji: '📝' },
  { value: 'feature', label: 'Feature', emoji: '✨' },
]

/** Why a comment wasn't saved, in the user's words. */
export function feedbackErrorText(err: unknown, t: Translate): string {
  if (err instanceof ForbiddenError) return t('feedback.errorForbidden')
  if (err instanceof TooManyTriesError) return t('feedback.errorTooMany')
  if (err instanceof InvalidValueError) return err.message
  return t('feedback.errorSave')
}

export function kindMeta(kind: FeedbackKind) {
  return FEEDBACK_KINDS.find((k) => k.value === kind) ?? FEEDBACK_KINDS[2]
}

const cell = (value: string | null | undefined) =>
  (value ?? '').replace(/\\/g, '\\\\').replace(/\s+/g, ' ').replace(/\|/g, '\\|').trim()

/** True only for safe in-app paths (`/dashboard`, `/strategies/123`), never `//evil` or `javascript:`. */
export function isAppPath(route: string | null | undefined): route is string {
  return Boolean(route && /^\/(?!\/)(?!.*\/\/)[^\\\s]*$/.test(route) && !hasControlChar(route))
}

function hasControlChar(value: string): boolean {
  for (let i = 0; i < value.length; i++) {
    const code = value.charCodeAt(i)
    if (code <= 0x1f || code === 0x7f) return true
  }
  return false
}

/** Format open feedback items as a Markdown table ready to paste into an issue or AI agent. */
export function feedbackMarkdown(items: FeedbackItem[]): string {
  const open = items.filter((item) => item.status === 'open')
  if (open.length === 0) return 'No open feedback items.'

  const lines = [
    `# Open feedback (${open.length})`,
    '',
    '| # | Kind | User | Page | Element | Selected text | Comment |',
    '| - | ---- | ---- | ---- | ------- | ------------- | ------- |',
  ]
  open.forEach((item, index) => {
    const page = item.route ? `\`${cell(item.route)}\`` : '—'
    const element = item.element ? `\`${cell(item.element)}\`` : '—'
    const quote = item.selectedText ? `"${cell(item.selectedText)}"` : '—'
    lines.push(
      `| ${index + 1} | ${item.kind} | ${cell(item.userEmail)} | ${page} | ${element} | ${quote} | ${cell(item.text)} |`,
    )
  })
  return lines.join('\n')
}

const STOP_TAGS = new Set(['MAIN', 'HEADER', 'NAV', 'BODY', 'HTML'])

/** Build a short, readable DOM path (`main > section.card > p:nth-of-type(2)`) for a selection node. */
export function elementPath(node: Node | null): string | null {
  const start = node instanceof Element ? node : (node?.parentElement ?? null)
  if (!start) return null

  const parts: string[] = []
  let el: Element | null = start
  while (el && parts.length < 5) {
    const tag = el.tagName.toLowerCase()
    if (el.id) {
      parts.unshift(`${tag}#${el.id}`)
      break
    }
    const classes = Array.from(el.classList)
      .filter((c) => !c.startsWith('_'))
      .slice(0, 2)
    let part = classes.length ? `${tag}.${classes.join('.')}` : tag
    const parent: Element | null = el.parentElement
    if (parent && !classes.length) {
      const tagName = el.tagName
      const sameTag = Array.from(parent.children).filter((c) => c.tagName === tagName)
      if (sameTag.length > 1) part += `:nth-of-type(${sameTag.indexOf(el) + 1})`
    }
    parts.unshift(part)
    if (STOP_TAGS.has(el.tagName)) break
    el = parent
  }
  return parts.join(' > ')
}

export interface SelectionPosition {
  top: number
  bottom: number
  left: number
  width: number
}

export type ButtonSide = 'top' | 'bottom'

const BOTTOM_RESERVED = 110
const TOP_RESERVED = 72
const HEADER_GAP = 8

/**
 * Decide which edge of the screen the "Add comment" button should sit on so it never covers the
 * selected text or the phone's Copy menu (which opens just above or below the selection).
 *
 * Defaults to the bottom of the screen; moves below the sticky header when the selection reaches
 * the bottom zone and the top zone is freer.
 */
export function buttonSide(selection: SelectionPosition, viewportHeight: number, headerBottom = 0): ButtonSide {
  const bottomClear = selection.bottom < viewportHeight - BOTTOM_RESERVED
  if (bottomClear) return 'bottom'

  const topLimit = Math.max(TOP_RESERVED, headerBottom + HEADER_GAP + 48)
  const topClear = selection.top > topLimit
  if (topClear) return 'top'

  const spaceAbove = selection.top - Math.max(0, headerBottom)
  const spaceBelow = viewportHeight - selection.bottom
  return spaceAbove > spaceBelow ? 'top' : 'bottom'
}

/** Pixel `top` offset for the button when placed at the top of the screen, clearing any sticky header. */
export function topOffset(headerBottom = 0): number {
  return Math.max(16, Math.round(headerBottom) + HEADER_GAP)
}
