import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  api,
  InvalidValueError,
  UnauthorizedError,
  type FeedbackItem,
  type FeedbackKind,
} from '../../api'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { feedbackMarkdown, isAppPath, kindMeta } from './feedback'
import { FEEDBACK_SUBMITTED_EVENT } from './FeedbackCapture'
import styles from './Feedback.module.css'
import { KindPicker } from './KindPicker'

type Filter = 'open' | 'closed' | 'all'

/**
 * Owner feedback review box: shown on `/admin` and on `/feedback`. Lists all feedback submitted by
 * confirmed users, filters by open/closed/all, exports open items as a Markdown table, and lets the
 * owner add, edit, close/reopen, or delete items.
 */
export function FeedbackSection() {
  const [items, setItems] = useState<FeedbackItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [filter, setFilter] = useState<Filter>('open')

  const [newKind, setNewKind] = useState<FeedbackKind>('note')
  const [newText, setNewText] = useState('')
  const [adding, setAdding] = useState(false)

  const [editingId, setEditingId] = useState<string | null>(null)
  const [editKind, setEditKind] = useState<FeedbackKind>('note')
  const [editText, setEditText] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)

  const [copied, setCopied] = useState(false)

  const load = useCallback(async () => {
    try {
      const data = await api.getFeedback()
      setItems(data)
      setError(null)
    } catch (e) {
      if (e instanceof UnauthorizedError) return
      setError('Could not load feedback.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    let live = true
    void api
      .getFeedback()
      .then((data) => {
        if (!live) return
        setItems(data)
        setError(null)
        setLoading(false)
      })
      .catch((e: unknown) => {
        if (!live || e instanceof UnauthorizedError) return
        setError('Could not load feedback.')
        setLoading(false)
      })
    return () => {
      live = false
    }
  }, [])

  useEffect(() => {
    const onSubmitted = () => {
      void load()
    }
    window.addEventListener(FEEDBACK_SUBMITTED_EVENT, onSubmitted)
    return () => window.removeEventListener(FEEDBACK_SUBMITTED_EVENT, onSubmitted)
  }, [load])

  useEffect(() => {
    if (!copied) return
    const id = window.setTimeout(() => setCopied(false), 2000)
    return () => window.clearTimeout(id)
  }, [copied])

  const openCount = items.filter((i) => i.status === 'open').length
  const closedCount = items.filter((i) => i.status === 'closed').length
  const visible = items.filter((i) => (filter === 'all' ? true : i.status === filter))

  const addComment = async (e: FormEvent) => {
    e.preventDefault()
    const trimmed = newText.trim()
    if (!trimmed || adding) return
    setAdding(true)
    setError(null)
    try {
      const created = await api.submitFeedback({
        kind: newKind,
        text: trimmed,
        route: null,
        element: null,
        selectedText: null,
      })
      setItems((prev) => [created, ...prev])
      setNewText('')
      setNewKind('note')
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      setError(err instanceof InvalidValueError ? err.message : 'Could not save comment.')
    } finally {
      setAdding(false)
    }
  }

  const startEdit = (item: FeedbackItem) => {
    setEditingId(item.id)
    setEditKind(item.kind)
    setEditText(item.text)
  }

  const saveEdit = async (id: string) => {
    const trimmed = editText.trim()
    if (!trimmed) return
    setBusyId(id)
    setError(null)
    try {
      const updated = await api.updateFeedback(id, { kind: editKind, text: trimmed })
      setItems((prev) => prev.map((i) => (i.id === id ? updated : i)))
      setEditingId(null)
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      setError(err instanceof InvalidValueError ? err.message : 'Could not update comment.')
    } finally {
      setBusyId(null)
    }
  }

  const toggleStatus = async (item: FeedbackItem) => {
    const nextStatus = item.status === 'open' ? 'closed' : 'open'
    setBusyId(item.id)
    setError(null)
    try {
      const updated = await api.updateFeedback(item.id, { status: nextStatus })
      setItems((prev) => prev.map((i) => (i.id === item.id ? updated : i)))
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      setError('Could not update status.')
    } finally {
      setBusyId(null)
    }
  }

  const remove = async (id: string) => {
    setBusyId(id)
    setError(null)
    try {
      await api.deleteFeedback(id)
      setItems((prev) => prev.filter((i) => i.id !== id))
      if (editingId === id) setEditingId(null)
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      setError('Could not delete comment.')
    } finally {
      setBusyId(null)
    }
  }

  const copyMarkdown = async () => {
    const md = feedbackMarkdown(items)
    try {
      await navigator.clipboard.writeText(md)
      setCopied(true)
    } catch {
      setError('Clipboard copy failed.')
    }
  }

  return (
    <section className={styles.section} aria-label="Feedback">
      <div className={styles.sectionHeader}>
        <h2 className={styles.sectionTitle}>Feedback</h2>
        <button
          type="button"
          className={styles.button}
          disabled={openCount === 0}
          onClick={() => void copyMarkdown()}
        >
          {copied ? 'Copied Markdown ✓' : `Copy open as Markdown (${openCount})`}
        </button>
      </div>

      <form className={styles.block} onSubmit={(e) => void addComment(e)}>
        <KindPicker value={newKind} onChange={setNewKind} />
        <textarea
          aria-label="New comment"
          className={styles.textarea}
          rows={2}
          value={newText}
          onChange={(e) => setNewText(e.target.value)}
          placeholder="Add a bug, idea, note, or feature request (or select any text on any screen)…"
        />
        <div className={styles.actions}>
          <button
            type="submit"
            className={`${styles.button} ${styles.primaryButton}`}
            disabled={adding || !newText.trim()}
          >
            {adding ? 'Adding…' : 'Add comment'}
          </button>
        </div>
      </form>

      <div className={styles.toolbar}>
        <div className={styles.filterGroup} role="group" aria-label="Filter feedback">
          {(
            [
              { key: 'open', label: `Open (${openCount})` },
              { key: 'closed', label: `Closed (${closedCount})` },
              { key: 'all', label: `All (${items.length})` },
            ] as const
          ).map((f) => (
            <button
              key={f.key}
              type="button"
              aria-pressed={filter === f.key}
              className={styles.filterTab}
              onClick={() => setFilter(f.key)}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <div className={styles.block}>
          <p role="alert" className={styles.error}>
            {error}
          </p>
        </div>
      )}

      {loading ? (
        <p className={styles.empty}>Loading feedback…</p>
      ) : visible.length === 0 ? (
        <p className={styles.empty}>
          No {filter === 'all' ? '' : `${filter} `}feedback items yet. Select any text on any screen to add a comment.
        </p>
      ) : (
        <ul className={styles.list}>
          {visible.map((item) => {
            const meta = kindMeta(item.kind)
            const isEditing = editingId === item.id
            const isBusy = busyId === item.id
            return (
              <li
                key={item.id}
                className={styles.item}
                data-closed={item.status === 'closed' || undefined}
              >
                <div className={styles.itemHeader}>
                  <span className={styles.badge}>
                    <span aria-hidden="true">{meta.emoji}</span> {meta.label}
                  </span>
                  {item.status === 'closed' && <span className={styles.closedBadge}>Closed</span>}
                  <span className={styles.author}>{item.userEmail}</span>
                  <span>{new Date(item.createdAt).toLocaleString()}</span>
                  {item.route &&
                    (isAppPath(item.route) ? (
                      <Link to={item.route} className={styles.routeLink}>
                        {item.route}
                      </Link>
                    ) : (
                      <code className={styles.code}>{item.route}</code>
                    ))}
                  {item.element && <code className={styles.code}>{item.element}</code>}
                </div>

                {item.selectedText && (
                  <blockquote className={styles.quote}>“{item.selectedText}”</blockquote>
                )}

                {isEditing ? (
                  <div className={styles.field}>
                    <KindPicker value={editKind} onChange={setEditKind} />
                    <textarea
                      aria-label="Edit comment"
                      className={styles.textarea}
                      rows={3}
                      value={editText}
                      onChange={(e) => setEditText(e.target.value)}
                    />
                    <div className={styles.itemActions}>
                      <button
                        type="button"
                        className={`${styles.button} ${styles.primaryButton}`}
                        disabled={isBusy || !editText.trim()}
                        onClick={() => void saveEdit(item.id)}
                      >
                        Save
                      </button>
                      <button
                        type="button"
                        className={styles.button}
                        disabled={isBusy}
                        onClick={() => setEditingId(null)}
                      >
                        Cancel
                      </button>
                    </div>
                  </div>
                ) : (
                  <p className={styles.commentText}>{item.text}</p>
                )}

                {!isEditing && (
                  <div className={styles.itemActions}>
                    <button
                      type="button"
                      className={styles.button}
                      disabled={isBusy}
                      onClick={() => startEdit(item)}
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      className={styles.button}
                      disabled={isBusy}
                      onClick={() => void toggleStatus(item)}
                    >
                      {item.status === 'open' ? 'Close' : 'Reopen'}
                    </button>
                    <button
                      type="button"
                      className={`${styles.button} ${styles.dangerButton}`}
                      disabled={isBusy}
                      onClick={() => void remove(item.id)}
                    >
                      Delete
                    </button>
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}

/** Dedicated owner feedback screen (`/feedback`). */
export function FeedbackPage() {
  return (
    <Screen top={<TopBar title="STL // FEEDBACK" back="/admin" />}>
      <PageHeader
        title="Feedback"
        lead="Comments submitted by users with a confirmed email from selected text across the app."
      />
      <FeedbackSection />
    </Screen>
  )
}
