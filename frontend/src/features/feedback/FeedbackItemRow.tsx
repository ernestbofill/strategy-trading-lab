import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { FeedbackItem, FeedbackKind } from '../../api'
import { isAppPath, kindMeta } from './feedback'
import { KindPicker } from './KindPicker'
import styles from './Feedback.module.css'
import { Button } from '../../components/Button'

interface FeedbackItemRowProps {
  item: FeedbackItem
  /** Its edit form is open (one item at a time). */
  editing: boolean
  /** A change to it is being saved. */
  busy: boolean
  onEdit: () => void
  onCancel: () => void
  onSave: (kind: FeedbackKind, text: string) => Promise<void>
  onToggle: () => Promise<void>
  onDelete: () => Promise<void>
}

/** One feedback item on `/feedback`: who, where and what, and the owner's edit, close or reopen, and delete. */
export function FeedbackItemRow({ item, editing, busy, onEdit, onCancel, onSave, onToggle, onDelete }: FeedbackItemRowProps) {
  const meta = kindMeta(item.kind)
  const [kind, setKind] = useState<FeedbackKind>(item.kind)
  const [text, setText] = useState(item.text)
  const startEdit = () => {
    setKind(item.kind)
    setText(item.text)
    onEdit()
  }
  return (
    <li
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

      {editing ? (
        <div className={styles.field}>
          <KindPicker value={kind} onChange={setKind} />
          <textarea
            aria-label="Edit comment"
            className={styles.textarea}
            rows={3}
            maxLength={4000}
            value={text}
            onChange={(e) => setText(e.target.value)}
          />
          <div className={styles.itemActions}>
            <Button
              variant="primary"
              disabled={busy || !text.trim()}
              onClick={() => void onSave(kind, text.trim())}
            >
              Save
            </Button>
            <Button
              variant="outline"
              disabled={busy}
              onClick={onCancel}
            >
              Cancel
            </Button>
          </div>
        </div>
      ) : (
        <p className={styles.commentText}>{item.text}</p>
      )}

      {!editing && (
        <div className={styles.itemActions}>
          <Button
            variant="outline"
            disabled={busy}
            onClick={startEdit}
          >
            Edit
          </Button>
          <Button
            variant="outline"
            disabled={busy}
            onClick={() => void onToggle()}
          >
            {item.status === 'open' ? 'Close' : 'Reopen'}
          </Button>
          <Button
            variant="outline-danger"
            disabled={busy}
            onClick={() => void onDelete()}
          >
            Delete
          </Button>
        </div>
      )}
    </li>
  )
}
