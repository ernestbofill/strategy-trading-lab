import { useEffect, useState } from 'react'
import { api, UnauthorizedError, type AdminAsset, type AssetKind } from '../../api'
import { PageHeader } from '../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import styles from './AdminAssets.module.css'
import { DataTable, type DataTableColumn, type DataTableFilter } from '../../components/DataTable'
import { AddAssetForm, EditAssetForm } from './AssetForms'

type State = { status: 'loading' } | { status: 'error' } | { status: 'ready'; assets: AdminAsset[] }

const kindText: Record<AssetKind, string> = {
  stock: 'Stock',
  fund: 'Fund',
  index: 'Index',
}

const columns: DataTableColumn<AdminAsset>[] = [
  {
    id: 'name',
    header: 'Asset',
    accessorFn: (a) => a.name,
    cell: (a) => (
      <div className={styles.assetCell}>
        <span>{a.name}</span>
        <span className={styles.what}>{a.what}</span>
      </div>
    ),
  },
  { id: 'symbol', header: 'Symbol', accessorFn: (a) => a.symbol, mono: true },
  { id: 'kind', header: 'Kind', accessorFn: (a) => a.kind, cell: (a) => kindText[a.kind] },
  { id: 'exchange', header: 'Exchange', accessorFn: (a) => a.exchange, mono: true },
  { id: 'currency', header: 'Currency', accessorFn: (a) => a.currency, mono: true },
  { id: 'strategies', header: 'Strategies', accessorFn: (a) => a.strategies, mono: true, align: 'right' },
  {
    id: 'latestPriceDate',
    header: 'Latest price',
    accessorFn: (a) => a.latestPriceDate ?? '',
    cell: (a) => a.latestPriceDate ?? '–',
    mono: true,
  },
  {
    id: 'status',
    header: 'Status',
    accessorFn: (a) => (a.active ? 'active' : 'hidden'),
    cell: (a) => <span className={a.active ? styles.active : styles.hidden}>{a.active ? 'Active' : 'Hidden'}</span>,
  },
]

const filters: DataTableFilter[] = [
  {
    columnId: 'kind',
    label: 'Kind',
    allLabel: 'All kinds',
    options: [
      { value: 'stock', label: 'Stocks' },
      { value: 'fund', label: 'Funds' },
      { value: 'index', label: 'Indexes' },
    ],
  },
  {
    columnId: 'status',
    label: 'Status',
    allLabel: 'All statuses',
    options: [
      { value: 'active', label: 'Active' },
      { value: 'hidden', label: 'Hidden' },
    ],
  },
]

/** Admin assets page (/admin/assets): all stocks, funds and indexes in the database, with search, filters, hide/show, adding by symbol and editing. */
export function AdminAssets() {
  const [state, setState] = useState<State>({ status: 'loading' })
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [editing, setEditing] = useState<AdminAsset | null>(null)

  const reload = async () => setState({ status: 'ready', assets: await api.getAdminAssets() })

  useEffect(() => {
    let live = true
    api
      .getAdminAssets()
      .then((assets) => live && setState({ status: 'ready', assets }))
      .catch(() => live && setState({ status: 'error' }))
    return () => {
      live = false
    }
  }, [])

  const toggleActive = async (asset: AdminAsset) => {
    setBusyId(asset.id)
    setError(null)
    try {
      await api.setAdminAssetActive(asset.id, !asset.active)
      await reload()
    } catch (e) {
      if (e instanceof UnauthorizedError) return
      setError("Couldn't update the asset. Try again.")
    } finally {
      setBusyId(null)
    }
  }

  return (
    <Screen top={<TopBar back="/admin" step="ADMIN" />}>
      <PageHeader title="Assets" lead="Stocks, funds and indexes. Prices are fetched daily for those a running strategy watches." />
      {state.status === 'loading' && <ScreenMessage>Loading the assets…</ScreenMessage>}
      {state.status === 'error' && <ScreenMessage>Could not load the assets. Try again later.</ScreenMessage>}
      {error && (
        <p role="alert" className={styles.error}>
          {error}
        </p>
      )}
      {state.status === 'ready' && !editing && <AddAssetForm onAdded={reload} />}
      {editing && (
        <EditAssetForm
          key={editing.id}
          asset={editing}
          onDone={async (saved) => {
            if (saved) await reload()
            setEditing(null)
          }}
        />
      )}
      {state.status === 'ready' && (
        <DataTable
          label="Assets"
          data={state.assets}
          getRowId={(a) => a.id}
          columns={columns}
          getSearchText={(a) => a.what}
          filters={filters}
          searchPlaceholder="Search by name, symbol or exchange…"
          initialSort={{ id: 'name' }}
          actionHeader="Actions"
          rowAction={(a) => (
            <div className={styles.buttons}>
              <button type="button" className={`ui-strong ${styles.actionButton}`} onClick={() => setEditing(a)}>
                Edit
                <span className="visually-hidden"> {a.name}</span>
              </button>
              <button
                type="button"
                className={`ui-strong ${styles.actionButton}`}
                disabled={busyId === a.id}
                onClick={() => void toggleActive(a)}
              >
                {a.active ? 'Hide' : 'Show'}
                <span className="visually-hidden"> {a.name}</span>
              </button>
            </div>
          )}
        />
      )}
    </Screen>
  )
}
