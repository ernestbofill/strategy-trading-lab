import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { FeedbackItem, FeedbackKind, FeedbackStatus } from '../../api'
import { feedbackItemMarkdown, isAppPath, kindMeta, statusActions } from './feedback'
import { useCopy } from '../../components/useCopy'
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
  onStatus: (status: FeedbackStatus) => Promise<void>
  onDelete: () => Promise<void>
}

/** One feedback item on `/feedback`: who, where and what, and the owner's edit, copy, close, validate or reopen, and delete. */
export function FeedbackItemRow({ item, editing, busy, onEdit, onCancel, onSave, onStatus, onDelete }: FeedbackItemRowProps) {
  const meta = kindMeta(item.kind)
  const { copied, copy } = useCopy()
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
      data-status={item.status}
    >
      <div className={styles.itemHeader}>
        <span className={styles.badge}>
          <span aria-hidden="true">{meta.emoji}</span> {meta.label}
        </span>
        {item.status !== 'open' && (
          <span className={styles.statusBadge}>{item.status === 'closed' ? 'Closed' : 'Validated'}</span>
        )}
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
          <Button variant="outline" onClick={() => void copy(feedbackItemMarkdown(item))}>
            {copied === true ? 'Copied ✓' : copied === false ? 'Copy failed' : 'Copy as Markdown'}
          </Button>
          {statusActions(item.status).map((action) => (
            <Button
              key={action.status}
              variant="outline"
              disabled={busy}
              onClick={() => void onStatus(action.status)}
            >
              {action.label}
            </Button>
          ))}
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
