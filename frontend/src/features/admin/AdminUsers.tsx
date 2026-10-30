import { useEffect, useState } from 'react'
import { api, type AdminUser, type AdminUsers as AdminUsersPage, type Role } from '../../api'
import { DataTable, type DataTableColumn, type DataTableFilter } from '../../components/DataTable'
import { PageHeader } from '../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { isoDate } from '../../lib/format'
import styles from './AdminUsers.module.css'

type State = { status: 'loading' } | { status: 'error' } | { status: 'ready'; page: AdminUsersPage }

const roleText: Record<Role, string> = { user: 'User', vip: 'VIP', owner: 'Owner' }

/** "2026-10-30" in the owner's time zone, or "–". */
const day = (at: string | null) => (at ? isoDate(new Date(at)) : '–')

const columns: DataTableColumn<AdminUser>[] = [
  { id: 'email', header: 'Email', accessorFn: (u) => u.email },
  { id: 'role', header: 'Role', accessorFn: (u) => u.role, cell: (u) => roleText[u.role] },
  {
    id: 'confirmed',
    header: 'Email confirmed',
    accessorFn: (u) => (u.confirmed ? 'yes' : 'no'),
    cell: (u) => <span className={u.confirmed ? styles.yes : styles.no}>{u.confirmed ? 'Yes' : 'No'}</span>,
  },
  { id: 'joined', header: 'Joined', accessorFn: (u) => u.joined, cell: (u) => day(u.joined), mono: true },
  { id: 'lastVisit', header: 'Last visit', accessorFn: (u) => u.lastVisit ?? '', cell: (u) => day(u.lastVisit), mono: true },
  { id: 'running', header: 'Running', accessorFn: (u) => u.running, mono: true, align: 'right' },
  { id: 'archived', header: 'Archived', accessorFn: (u) => u.archived, mono: true, align: 'right' },
]

const filters: DataTableFilter[] = [
  {
    columnId: 'role',
    label: 'Role',
    allLabel: 'All roles',
    options: [
      { value: 'user', label: 'Users' },
      { value: 'vip', label: 'VIPs' },
      { value: 'owner', label: 'Owners' },
    ],
  },
  {
    columnId: 'confirmed',
    label: 'Email confirmed',
    allLabel: 'Confirmed or not',
    options: [
      { value: 'yes', label: 'Confirmed' },
      { value: 'no', label: 'Not confirmed' },
    ],
  },
]

/** The counts above the list: accounts, confirmed ones, and running strategies by template and mode. */
function Counts({ counts }: { counts: AdminUsersPage['counts'] }) {
  return (
    <div className={`well ${styles.counts}`}>
      <dl className={styles.totals}>
        <div>
          <dt>Accounts</dt>
          <dd>{counts.users}</dd>
        </div>
        <div>
          <dt>Confirmed</dt>
          <dd>{counts.confirmed}</dd>
        </div>
      </dl>
      <table className={styles.running}>
        <caption className="ui-strong">Running strategies</caption>
        <thead>
          <tr>
            <th scope="col">Template</th>
            <th scope="col">Simulation</th>
            <th scope="col">Live</th>
          </tr>
        </thead>
        <tbody>
          {counts.running.map((t) => (
            <tr key={t.templateKey}>
              <th scope="row">{t.templateName}</th>
              <td>{t.simulation}</td>
              <td>{t.live}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/** Admin users page (/admin/users): every account with its role, confirmed email, last visit and strategies. Read-only for now. */
export function AdminUsers() {
  const [state, setState] = useState<State>({ status: 'loading' })

  useEffect(() => {
    let live = true
    api
      .getAdminUsers()
      .then((page) => live && setState({ status: 'ready', page }))
      .catch(() => live && setState({ status: 'error' }))
    return () => {
      live = false
    }
  }, [])

  return (
    <Screen top={<TopBar back="/admin" step="ADMIN" />}>
      <PageHeader title="Users" lead="Every account, newest first. Blocking, deleting and invites come later." />
      {state.status === 'loading' && <ScreenMessage>Loading the users…</ScreenMessage>}
      {state.status === 'error' && <ScreenMessage>Could not load the users. Try again later.</ScreenMessage>}
      {state.status === 'ready' && (
        <>
          <Counts counts={state.page.counts} />
          <DataTable
            label="Users"
            data={state.page.users}
            getRowId={(u) => u.id}
            columns={columns}
            filters={filters}
            searchPlaceholder="Search by email…"
            initialSort={{ id: 'joined', desc: true }}
          />
        </>
      )}
    </Screen>
  )
}
