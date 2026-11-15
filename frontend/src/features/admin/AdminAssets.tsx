import { useState } from 'react'
import { api, UnauthorizedError, type AdminAsset, type AssetKind } from '../../api'
import { PageHeader } from '../../components/PageHeader'
import { Loaded } from '../../components/Loaded'
import { Screen } from '../../components/Screen'
import { reload, useLoadable } from '../../lib/useLoadable'
import { AdminTopBar } from './AdminTopBar'
import { adminCacheKeys } from './cacheKeys'
import styles from './AdminAssets.module.css'
import { DataTable, type DataTableColumn, type DataTableFilter } from '../../components/DataTable'
import { AddAssetForm, EditAssetForm } from './AssetForms'
import { AllPricesSection } from './AssetPrices'

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
    cell: (a) =>
      a.pricesProblem ? (
        <div className={styles.assetCell}>
          <span>{a.latestPriceDate ?? '–'}</span>
          <span className={styles.problem}>{a.pricesProblem}</span>
        </div>
      ) : (
        (a.latestPriceDate ?? '–')
      ),
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
  const state = useLoadable(adminCacheKeys.assets, api.getAdminAssets)
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [editing, setEditing] = useState<AdminAsset | null>(null)

  // After a change that worked: the list stays on screen while it loads again, and a failed reload is said on its own, not
  // as if the change had failed.
  const reloadAssets = async () => {
    if (!(await reload(adminCacheKeys.assets, { fresh: true }))) setError("Couldn't reload the assets. Reload the page.")
  }

  const toggleActive = async (asset: AdminAsset) => {
    setBusyId(asset.id)
    setError(null)
    try {
      await api.setAdminAssetActive(asset.id, !asset.active)
      await reloadAssets()
    } catch (e) {
      if (e instanceof UnauthorizedError) return
      setError("Couldn't update the asset. Try again.")
    } finally {
      setBusyId(null)
    }
  }

  return (
    <Screen top={<AdminTopBar />}>
      <PageHeader title="Assets" lead="Stocks, funds and indexes. Prices are fetched daily for those a running strategy watches, and for the index fund (SPY) always." />
      <Loaded state={state} what="the assets" english>
        {(assets) => (
          <>
            {error && (
              <p role="alert" className={styles.error}>
                {error}
              </p>
            )}
            {!editing && <AddAssetForm onAdded={reloadAssets} />}
            {/* Shown while editing too: a running Download all keeps its progress and summary. */}
            <AllPricesSection assets={assets} onChanged={reloadAssets} />
            {editing && (
              <EditAssetForm
                key={editing.id}
                asset={editing}
                onDone={async (saved) => {
                  if (saved) await reloadAssets()
                  setEditing(null)
                }}
                onPricesChanged={reloadAssets}
              />
            )}
            <DataTable
              label="Assets"
              data={assets}
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
          </>
        )}
      </Loaded>
    </Screen>
  )
}
