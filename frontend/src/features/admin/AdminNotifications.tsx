import { useState, type FormEvent } from 'react'
import { api, TooManyTriesError, type AdminPush, type AdminPushRecipient, type NotificationKind } from '../../api'
import { FormMessage, SelectField, TextAreaField, TextField } from '../../components/Field'
import { DataTable, type DataTableColumn, type DataTableFilter } from '../../components/DataTable'
import { PageHeader } from '../../components/PageHeader'
import { Loaded } from '../../components/Loaded'
import { Screen } from '../../components/Screen'
import { Button } from '../../components/Button'
import { useSubmit } from '../../components/useSubmit'
import { localDateTime } from '../../lib/format'
import { reload, useLoadable } from '../../lib/useLoadable'
import { AdminTopBar } from './AdminTopBar'
import { adminCacheKeys } from './cacheKeys'
import styles from './AdminInvites.module.css'

/** The backend's limits (Notifications/PushTexts.cs). */
const TITLE_MAX = 200
const BODY_MAX = 1000

const kindText: Record<NotificationKind, string> = {
  signal: 'Signal',
  snoozed: 'Reminder',
  overview: 'Summary',
  aiRound: 'AI round',
  signalEnded: 'Signal ended',
  stockCandidate: 'New candidate',
  stockClose: 'Getting close',
  test: 'Test',
}

type Result = 'sent' | 'failed' | 'skipped'
const resultOf = (p: AdminPush): Result => (p.sentAt ? 'sent' : p.failedAt ? 'failed' : 'skipped')
const resultText: Record<Result, string> = { sent: 'Sent', failed: 'Failed', skipped: 'Not sent' }

const columns: DataTableColumn<AdminPush>[] = [
  // Older pushes have no title kept: the kind names the row instead.
  { id: 'title', header: 'Title', accessorFn: (p) => p.title ?? kindText[p.kind] },
  { id: 'body', header: 'Text', accessorFn: (p) => p.body ?? '', cell: (p) => p.body ?? '–' },
  { id: 'email', header: 'User', accessorFn: (p) => p.email },
  { id: 'kind', header: 'Kind', accessorFn: (p) => p.kind, cell: (p) => kindText[p.kind] },
  {
    id: 'result',
    header: 'Result',
    accessorFn: resultOf,
    cell: (p) => (p.error ? `${resultText[resultOf(p)]}: ${p.error}` : resultText[resultOf(p)]),
  },
  { id: 'createdAt', header: 'When', accessorFn: (p) => p.createdAt, cell: (p) => localDateTime(p.createdAt), mono: true },
]

const filters: DataTableFilter[] = [
  {
    columnId: 'kind',
    label: 'Kind',
    allLabel: 'All kinds',
    // The summary goes by email only: never in this list.
    options: Object.entries(kindText).filter(([value]) => value !== 'overview').map(([value, label]) => ({ value, label })),
  },
  {
    columnId: 'result',
    label: 'Result',
    allLabel: 'Any result',
    options: Object.entries(resultText).map(([value, label]) => ({ value, label })),
  },
]

/**
 * Notifications (/admin/notifications, owner only; spec: Admin, Notifications): a test push with a title and text to a
 * user's devices, and the pushes sent, newest first.
 */
export function AdminNotifications() {
  const state = useLoadable(adminCacheKeys.notifications, api.getAdminNotifications)
  const [reloadFailed, setReloadFailed] = useState(false)
  const load = async () => setReloadFailed(!(await reload(adminCacheKeys.notifications, { fresh: true })))

  return (
    <Screen top={<AdminTopBar />}>
      <PageHeader title="Notifications" lead="Push notifications sent to users' devices. Titles and texts are kept from 2026-11-24." />
      <Loaded state={state} what="the notifications" english>
        {(page) => (
          <>
            <section className={styles.group} aria-labelledby="test-push">
              <h2 id="test-push" className={styles.groupTitle}>
                Test push
              </h2>
              <TestPushForm recipients={page.recipients} onSent={load} />
            </section>
            <section className={styles.group} aria-labelledby="pushes-sent">
              <h2 id="pushes-sent" className={styles.groupTitle}>
                Sent
              </h2>
              {reloadFailed && <FormMessage tone="error">Couldn't reload the list. Reload the page.</FormMessage>}
              <DataTable
                label="Pushes sent"
                data={page.pushes}
                getRowId={(p) => p.id}
                columns={columns}
                filters={filters}
                searchPlaceholder="Search pushes…"
                initialSort={{ id: 'createdAt', desc: true }}
                emptyMessage="No pushes yet."
              />
            </section>
          </>
        )}
      </Loaded>
    </Screen>
  )
}

function TestPushForm({ recipients, onSent }: { recipients: AdminPushRecipient[]; onSent: () => Promise<void> }) {
  const [userId, setUserId] = useState(recipients[0]?.id ?? '')
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const submit = useSubmit((err) => (err instanceof TooManyTriesError ? 'Too many test pushes this hour. Try again later.' : "Couldn't send it. Try again."))
  const { busy, errors, message } = submit

  if (recipients.length === 0) return <p className={styles.muted}>No user has notifications on for a device.</p>

  const send = async (e: FormEvent) => {
    e.preventDefault()
    const sent: { push?: AdminPush } = {}
    const worked = await submit.run(
      async () => {
        sent.push = await api.sendAdminTestPush({ userId, title: title.trim(), body: body.trim() })
      },
      () => ({
        ...(title.trim() ? {} : { title: 'Write a title.' }),
        ...(title.trim().length > TITLE_MAX ? { title: `Up to ${TITLE_MAX} characters.` } : {}),
        ...(body.trim() ? {} : { body: 'Write a text.' }),
      }),
    )
    const push = sent.push
    if (!worked || !push) return
    await onSent()
    if (push.sentAt) submit.done(`Sent to ${push.email}.`)
    else {
      submit.reset()
      submit.setMessage({ tone: 'error', text: `Not delivered: ${push.error ?? 'no device got it.'}` })
    }
  }

  return (
    <form className={styles.form} onSubmit={send} noValidate>
      <SelectField
        label="User"
        value={userId}
        onChange={setUserId}
        options={recipients.map((r) => ({ value: r.id, label: `${r.email} (${r.devices} ${r.devices === 1 ? 'device' : 'devices'})` }))}
      />
      <TextField label="Title" autoComplete="off" value={title} onChange={setTitle} error={errors.title} />
      <TextAreaField label="Text" value={body} onChange={setBody} rows={3} maxLength={BODY_MAX} error={errors.body} />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <Button type="submit" variant="primary" disabled={busy}>
        {busy ? 'Sending…' : 'Send test push'}
      </Button>
    </form>
  )
}
