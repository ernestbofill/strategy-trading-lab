import { useCallback, useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { useLocation } from 'react-router-dom'
import {
  api,
  ForbiddenError,
  InvalidValueError,
  TooManyTriesError,
  UnauthorizedError,
  type FeedbackKind,
} from '../../api'
import { useT } from '../../i18n/I18n'
import { useCurrentAccount } from '../account/AccountContext'
import { buttonSide, elementPath, topOffset, type ButtonSide, type SelectionPosition } from './feedback'
import styles from './Feedback.module.css'
import { KindPicker } from './KindPicker'

interface CapturedSelection extends SelectionPosition {
  text: string
  element: string | null
  side: ButtonSide
  headerBottom: number
}

interface ContextMenuPos {
  x: number
  y: number
}

const IGNORED_SELECTOR = 'input, textarea, select, [contenteditable="true"], [data-feedback-ignore]'

function isInsideIgnored(node: Node | null): boolean {
  const el = node instanceof Element ? node : (node?.parentElement ?? null)
  return Boolean(el?.closest(IGNORED_SELECTOR))
}

function measureHeaderBottom(): number {
  const header = document.querySelector('header')
  if (!header) return 0
  return Math.max(0, header.getBoundingClientRect().bottom)
}

function readSelection(): CapturedSelection | null {
  const sel = window.getSelection()
  if (!sel || sel.isCollapsed || sel.rangeCount === 0) return null
  const text = sel.toString().trim()
  if (text.length < 2) return null

  const range = sel.getRangeAt(0)
  if (isInsideIgnored(range.commonAncestorContainer)) return null

  const rect = range.getBoundingClientRect?.() ?? { top: 0, bottom: 0, left: 0, width: 0 }
  const pos: SelectionPosition = {
    top: rect.top || 120,
    bottom: rect.bottom || 150,
    left: rect.left || window.innerWidth / 2,
    width: rect.width || 80,
  }
  const headerBottom = measureHeaderBottom()
  return {
    ...pos,
    text: text.slice(0, 1000),
    element: elementPath(range.commonAncestorContainer)?.slice(0, 300) ?? null,
    side: buttonSide(pos, window.innerHeight || 800, headerBottom),
    headerBottom,
  }
}

export const FEEDBACK_SUBMITTED_EVENT = 'stl:feedback-submitted'

/**
 * Floating "Add comment" button + desktop right-click menu + modal dialog whenever a user with a
 * confirmed email selects text on any screen.
 */
export function FeedbackCapture() {
  const t = useT()
  const { emailVerified } = useCurrentAccount()
  const location = useLocation()
  const route = `${location.pathname}${location.search}`

  const [selection, setSelection] = useState<CapturedSelection | null>(null)
  const [contextMenu, setContextMenu] = useState<ContextMenuPos | null>(null)
  const [open, setOpen] = useState(false)
  const [kind, setKind] = useState<FeedbackKind>('note')
  const [text, setText] = useState('')
  const [saving, setSaving] = useState(false)
  const [savedToast, setSavedToast] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const dialogRef = useRef<HTMLDialogElement | null>(null)
  const textareaRef = useRef<HTMLTextAreaElement | null>(null)
  const openRef = useRef(open)
  useEffect(() => {
    openRef.current = open
  }, [open])

  const titleId = useId()
  const commentId = useId()

  useEffect(() => {
    if (!emailVerified) return

    let timer: number | undefined
    const update = () => {
      if (openRef.current) return
      const next = readSelection()
      setSelection(next)
      if (!next) setContextMenu(null)
    }
    const onSelectionChange = () => {
      window.clearTimeout(timer)
      timer = window.setTimeout(update, 120)
    }
    const onContextMenu = (e: MouseEvent) => {
      if (openRef.current || isInsideIgnored(e.target as Node | null)) return
      const current = readSelection()
      if (!current) return
      e.preventDefault()
      setSelection(current)
      setContextMenu({
        x: Math.min(e.clientX, Math.max(16, (window.innerWidth || 800) - 210)),
        y: Math.min(e.clientY, Math.max(16, (window.innerHeight || 600) - 120)),
      })
    }
    const dismissMenu = (e: MouseEvent) => {
      const target = e.target as Element | null
      if (target?.closest('[data-feedback-ignore]')) return
      setContextMenu(null)
    }

    document.addEventListener('selectionchange', onSelectionChange)
    document.addEventListener('contextmenu', onContextMenu)
    document.addEventListener('mousedown', dismissMenu)
    window.addEventListener('scroll', update, { passive: true })
    window.addEventListener('resize', update)
    return () => {
      window.clearTimeout(timer)
      document.removeEventListener('selectionchange', onSelectionChange)
      document.removeEventListener('contextmenu', onContextMenu)
      document.removeEventListener('mousedown', dismissMenu)
      window.removeEventListener('scroll', update)
      window.removeEventListener('resize', update)
    }
  }, [emailVerified])

  useEffect(() => {
    if (!open) return
    const dialog = dialogRef.current
    if (dialog) {
      if (typeof dialog.showModal === 'function' && !dialog.open) {
        try {
          dialog.showModal()
        } catch {
          dialog.setAttribute('open', '')
        }
      } else {
        dialog.setAttribute('open', '')
      }
    }
    textareaRef.current?.focus()
  }, [open])

  const closeModal = useCallback(() => {
    const dialog = dialogRef.current
    if (dialog?.open && typeof dialog.close === 'function') {
      try {
        dialog.close()
      } catch {
        dialog.removeAttribute('open')
      }
    }
    setOpen(false)
    setSelection(null)
    setContextMenu(null)
    setText('')
    setError(null)
    window.getSelection()?.removeAllRanges()
  }, [])

  const closeIfEmpty = useCallback(() => {
    if (!saving && !text.trim()) closeModal()
  }, [saving, text, closeModal])

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault()
        closeIfEmpty()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, closeIfEmpty])

  useEffect(() => {
    if (!savedToast) return
    const id = window.setTimeout(() => setSavedToast(false), 2200)
    return () => window.clearTimeout(id)
  }, [savedToast])

  if (!emailVerified) return null

  const openModal = () => {
    setKind('note')
    setText('')
    setError(null)
    setContextMenu(null)
    setOpen(true)
  }

  const copySelectedText = async () => {
    if (!selection) return
    try {
      await navigator.clipboard.writeText(selection.text)
    } catch {
      document.execCommand?.('copy')
    }
    setContextMenu(null)
    window.getSelection()?.removeAllRanges()
    setSelection(null)
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const trimmed = text.trim()
    if (!trimmed || saving) return

    setSaving(true)
    setError(null)
    try {
      await api.submitFeedback({
        kind,
        text: trimmed,
        route: route.slice(0, 300),
        element: selection?.element ?? null,
        selectedText: selection?.text ?? null,
      })
      window.dispatchEvent(new CustomEvent(FEEDBACK_SUBMITTED_EVENT))
      closeModal()
      setSavedToast(true)
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      setError(
        err instanceof ForbiddenError
          ? t('feedback.errorForbidden')
          : err instanceof TooManyTriesError
            ? t('feedback.errorTooMany')
            : err instanceof InvalidValueError
              ? err.message
              : t('feedback.errorSave'),
      )
    } finally {
      setSaving(false)
    }
  }

  return (
    <div data-feedback-ignore>
      {selection && !open && (
        <button
          type="button"
          className={styles.floatingButton}
          data-side={selection.side}
          style={
            selection.side === 'top'
              ? { top: `${topOffset(selection.headerBottom)}px` }
              : undefined
          }
          onMouseDown={(e) => e.preventDefault()}
          onClick={openModal}
        >
          💬 {t('feedback.addComment')}
        </button>
      )}

      {contextMenu && selection && !open && (
        <div
          role="menu"
          aria-label={t('feedback.selectionActions')}
          className={styles.contextMenu}
          style={{ left: `${contextMenu.x}px`, top: `${contextMenu.y}px` }}
          onMouseDown={(e) => e.preventDefault()}
        >
          <button type="button" role="menuitem" className={styles.contextMenuItem} onClick={openModal}>
            <span aria-hidden="true">💬</span> {t('feedback.addComment')}
          </button>
          <button
            type="button"
            role="menuitem"
            className={styles.contextMenuItem}
            onClick={() => void copySelectedText()}
          >
            <span aria-hidden="true">📋</span> {t('feedback.copy')}
          </button>
        </div>
      )}

      {open && selection && (
        <dialog
          ref={dialogRef}
          className={styles.dialog}
          aria-labelledby={titleId}
          onCancel={(e) => {
            e.preventDefault()
            closeIfEmpty()
          }}
          onClick={(e) => {
            if (e.target === e.currentTarget) closeIfEmpty()
          }}
        >
          <form className={styles.dialogBody} onSubmit={(e) => void submit(e)}>
            <h2 id={titleId} className={styles.dialogTitle}>
              {t('feedback.dialogTitle')}
            </h2>
            <KindPicker value={kind} onChange={setKind} />
            <p className={styles.metaLine}>
              {t('feedback.page')} <code className={styles.code}>{route}</code>
            </p>
            <blockquote className={styles.quote}>“{selection.text}”</blockquote>
            <div className={styles.field}>
              <label htmlFor={commentId} className={styles.label}>
                {t('feedback.yourComment')}
              </label>
              <textarea
                id={commentId}
                ref={textareaRef}
                className={styles.textarea}
                rows={3}
                maxLength={4000}
                value={text}
                onChange={(e) => setText(e.target.value)}
                placeholder={t('feedback.placeholder')}
                required
              />
            </div>
            {error && (
              <p role="alert" className={styles.error}>
                {error}
              </p>
            )}
            <div className={styles.actions}>
              <button
                type="button"
                className={styles.button}
                disabled={saving}
                onClick={closeModal}
              >
                {t('feedback.cancel')}
              </button>
              <button
                type="submit"
                className={`${styles.button} ${styles.primaryButton}`}
                disabled={saving || !text.trim()}
              >
                {saving ? t('feedback.saving') : t('feedback.save')}
              </button>
            </div>
          </form>
        </dialog>
      )}

      {savedToast && (
        <div role="status" className={styles.toast}>
          {t('feedback.saved')}
        </div>
      )}
    </div>
  )
}
