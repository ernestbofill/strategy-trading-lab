import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  api,
  InvalidValueError,
  UnauthorizedError,
  type FeedbackItem,
  type FeedbackKind,
} from '../../api'
import { PageHeader } from '../../components/PageHeader'
import { Screen } from '../../components/Screen'
import { AdminTopBar } from '../admin/AdminTopBar'
import { FEEDBACK_SUBMITTED_EVENT, feedbackMarkdown } from './feedback'
import { useCopy } from '../../components/useCopy'
import styles from './Feedback.module.css'
import { FeedbackItemRow } from './FeedbackItemRow'
import { KindPicker } from './KindPicker'
import { Button } from '../../components/Button'

type Filter = 'open' | 'closed' | 'all'

/**
 * Owner feedback review box: shown on `/feedback`. Lists all feedback submitted by
 * confirmed users, filters by open/closed/all, exports open items as a Markdown table, and lets the
 * owner add, edit, close/reopen, or delete items.
 */
interface FeedbackSectionProps {
  titleLink?: string
}

export function FeedbackSection({ titleLink }: FeedbackSectionProps = {}) {
  const [items, setItems] = useState<FeedbackItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [filter, setFilter] = useState<Filter>('open')

  const [newKind, setNewKind] = useState<FeedbackKind>('note')
  const [newText, setNewText] = useState('')
  const [adding, setAdding] = useState(false)

  const [editingId, setEditingId] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)

  const { copied, copy } = useCopy()

  useEffect(() => {
    let live = true
    const fetchFeedback = () => {
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
    }
    fetchFeedback()
    window.addEventListener(FEEDBACK_SUBMITTED_EVENT, fetchFeedback)
    return () => {
      live = false
      window.removeEventListener(FEEDBACK_SUBMITTED_EVENT, fetchFeedback)
    }
  }, [])

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

  /** One item's change: busy while it runs, its failure said above the list (an ended session goes to Log-in). */
  const act = async (id: string, failure: string, work: () => Promise<void>) => {
    setBusyId(id)
    setError(null)
    try {
      await work()
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      setError(err instanceof InvalidValueError ? err.message : failure)
    } finally {
      setBusyId(null)
    }
  }

  const replace = (updated: FeedbackItem) => setItems((prev) => prev.map((i) => (i.id === updated.id ? updated : i)))

  const saveEdit = (id: string, kind: FeedbackKind, text: string) =>
    act(id, 'Could not update comment.', async () => {
      replace(await api.updateFeedback(id, { kind, text }))
      setEditingId(null)
    })

  const toggleStatus = (item: FeedbackItem) =>
    act(item.id, 'Could not update status.', async () =>
      replace(await api.updateFeedback(item.id, { status: item.status === 'open' ? 'closed' : 'open' })),
    )

  const remove = (id: string) =>
    act(id, 'Could not delete comment.', async () => {
      await api.deleteFeedback(id)
      setItems((prev) => prev.filter((i) => i.id !== id))
      if (editingId === id) setEditingId(null)
    })

  const copyMarkdown = async () => {
    if (!(await copy(feedbackMarkdown(items)))) setError('Clipboard copy failed.')
  }

  return (
    <section className={styles.section} aria-label="Feedback">
      <div className={styles.sectionHeader}>
        <h2 className={styles.sectionTitle}>
          {titleLink ? (
            <Link to={titleLink} className={styles.sectionTitleLink}>
              Feedback
            </Link>
          ) : (
            'Feedback'
          )}
        </h2>
        <Button
          variant="outline"
          disabled={openCount === 0}
          onClick={() => void copyMarkdown()}
        >
          {copied === true ? 'Copied Markdown ✓' : `Copy open as Markdown (${openCount})`}
        </Button>
      </div>

      <form className={styles.block} onSubmit={(e) => void addComment(e)}>
        <KindPicker value={newKind} onChange={setNewKind} />
        <textarea
          aria-label="New comment"
          className={styles.textarea}
          rows={2}
          maxLength={4000}
          value={newText}
          onChange={(e) => setNewText(e.target.value)}
          placeholder="Add a bug, idea, note, or feature request (or select any text on any screen)…"
        />
        <div className={styles.actions}>
          <Button
            type="submit"
            variant="primary"
            disabled={adding || !newText.trim()}
          >
            {adding ? 'Adding…' : 'Add comment'}
          </Button>
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
          {visible.map((item) => (
            <FeedbackItemRow
              key={item.id}
              item={item}
              editing={editingId === item.id}
              busy={busyId === item.id}
              onEdit={() => setEditingId(item.id)}
              onCancel={() => setEditingId(null)}
              onSave={(kind, text) => saveEdit(item.id, kind, text)}
              onToggle={() => toggleStatus(item)}
              onDelete={() => remove(item.id)}
            />
          ))}
        </ul>
      )}
    </section>
  )
}

/** Dedicated owner feedback screen (`/feedback`). */
export function FeedbackPage() {
  return (
    <Screen top={<AdminTopBar />}>
      <PageHeader
        title="Feedback"
        lead="Comments submitted by users with a confirmed email from selected text across the app."
      />
      <FeedbackSection />
    </Screen>
  )
}
