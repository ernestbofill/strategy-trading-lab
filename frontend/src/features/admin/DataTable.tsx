import {
  columnFilteringFeature,
  createFilteredRowModel,
  createPaginatedRowModel,
  createSortedRowModel,
  filterFn_equalsString,
  filterFn_includesString,
  flexRender,
  globalFilteringFeature,
  rowPaginationFeature,
  rowSortingFeature,
  sortFn_alphanumeric,
  sortFn_basic,
  sortFn_text,
  tableFeatures,
  useTable,
  type ColumnDef,
  type ColumnFiltersState,
  type PaginationState,
  type RowData,
  type SortingState,
} from '@tanstack/react-table'
import { useId, useMemo, useState, type CSSProperties, type ReactNode } from 'react'
import styles from './DataTable.module.css'

const features = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: {
    alphanumeric: sortFn_alphanumeric,
    basic: sortFn_basic,
    text: sortFn_text,
  },
  columnFilteringFeature,
  globalFilteringFeature,
  filteredRowModel: createFilteredRowModel(),
  filterFns: {
    includesString: filterFn_includesString,
    equalsString: filterFn_equalsString,
  },
  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),
})

export interface DataTableColumn<T extends RowData> {
  /** Unique column id. */
  id: string
  /** Column header text (also shown as the field label in phone cards). */
  header: string
  /** Value used for sorting, searching and filtering. */
  accessorFn: (row: T) => string | number | boolean | null | undefined
  /** Custom cell rendering; defaults to rendering the accessed value. */
  cell?: (row: T) => ReactNode
  /** Whether clicking the column header sorts by this column (default true). */
  sortable?: boolean
  /** Render the cell in the mono font. */
  mono?: boolean
  /** Horizontal alignment on desktop. */
  align?: 'left' | 'right'
}

export interface DataTableFilter {
  /** Column id to filter by (exact match on the column's stringified value). */
  columnId: string
  /** Accessible label for the filter select. */
  label: string
  /** Option text when no filter is active ("All kinds"). */
  allLabel: string
  options: { value: string; label: string }[]
}

export interface DataTableProps<T extends RowData> {
  /** Accessible name for the table region ("Assets"). */
  label: string
  data: T[]
  getRowId: (row: T) => string
  /** Columns. The first column is the row header (`<th scope="row">`) and card title on phones. */
  columns: DataTableColumn<T>[]
  /** Additional text per row included in search matching. */
  getSearchText?: (row: T) => string
  /** Optional dropdown filters above the table. */
  filters?: DataTableFilter[]
  searchLabel?: string
  searchPlaceholder?: string
  /** Optional action control rendered for each row (last column on desktop, card footer on phones). */
  rowAction?: (row: T) => ReactNode
  actionHeader?: string
  initialSort?: { id: string; desc?: boolean }
  /** Rows per page (default 10). */
  pageSize?: number
  emptyMessage?: string
}

const ACTION_COLUMN_ID = '_action'

/**
 * Shared admin data table (TanStack Table): sort by column, search, filter, paginate, and a per-row action.
 * On narrow containers (< 640px), rows stack into cards with no horizontal scrolling.
 */
export function DataTable<T extends RowData>({
  label,
  data,
  getRowId,
  columns,
  getSearchText,
  filters = [],
  searchLabel = 'Search',
  searchPlaceholder = 'Search…',
  rowAction,
  actionHeader = 'Action',
  initialSort,
  pageSize = 10,
  emptyMessage = 'No matching rows.',
}: DataTableProps<T>) {
  const searchId = useId()
  const [sorting, setSorting] = useState<SortingState>(() =>
    initialSort ? [{ id: initialSort.id, desc: initialSort.desc ?? false }] : [],
  )
  const [globalFilter, setGlobalFilter] = useState('')
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([])
  const [pagination, setPagination] = useState<PaginationState>({ pageIndex: 0, pageSize })

  const columnById = useMemo(() => new Map(columns.map((c) => [c.id, c])), [columns])

  const tableColumns = useMemo<ColumnDef<typeof features, T, unknown>[]>(() => {
    const defs: ColumnDef<typeof features, T, unknown>[] = columns.map((col) => ({
      id: col.id,
      header: col.header,
      accessorFn: (row) => col.accessorFn(row) ?? '',
      enableSorting: col.sortable ?? true,
      sortDescFirst: false,
      filterFn: (row, columnId, filterValue: string) =>
        String(row.getValue(columnId) ?? '').toLowerCase() === filterValue.toLowerCase(),
      cell: ({ row }) => (col.cell ? col.cell(row.original) : String(col.accessorFn(row.original) ?? '–')),
    }))
    if (rowAction) {
      defs.push({
        id: ACTION_COLUMN_ID,
        header: actionHeader,
        enableSorting: false,
        enableGlobalFilter: false,
        cell: ({ row }) => rowAction(row.original),
      })
    }
    return defs
  }, [columns, rowAction, actionHeader])

  const table = useTable({
    features,
    data,
    columns: tableColumns,
    getRowId,
    state: { sorting, globalFilter, columnFilters, pagination },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    onColumnFiltersChange: setColumnFilters,
    onPaginationChange: setPagination,
    enableSortingRemoval: false,
    globalFilterFn: (row, _columnId, filterValue: string) => {
      const needle = filterValue.trim().toLowerCase()
      if (!needle) return true
      const haystack = [
        ...columns.map((c) => String(c.accessorFn(row.original) ?? '')),
        getSearchText ? getSearchText(row.original) : '',
      ]
        .join(' ')
        .toLowerCase()
      return haystack.includes(needle)
    },
  })

  const totalRows = table.getFilteredRowModel().rows.length
  const rows = table.getRowModel().rows
  const { pageIndex } = pagination
  const start = totalRows === 0 ? 0 : pageIndex * pagination.pageSize + 1
  const end = Math.min(totalRows, (pageIndex + 1) * pagination.pageSize)

  return (
    <div className={styles.container}>
      <div className={styles.toolbar}>
        <label htmlFor={searchId} className="visually-hidden">
          {searchLabel}
        </label>
        <input
          id={searchId}
          type="search"
          className={styles.searchInput}
          placeholder={searchPlaceholder}
          value={globalFilter}
          onChange={(e) => {
            setGlobalFilter(e.target.value)
            setPagination((p) => (p.pageIndex === 0 ? p : { ...p, pageIndex: 0 }))
          }}
        />
        {filters.length > 0 && (
          <div className={styles.filters}>
            {filters.map((f) => {
              const col = table.getColumn(f.columnId)
              const current = (col?.getFilterValue() as string | undefined) ?? ''
              return (
                <select
                  key={f.columnId}
                  aria-label={f.label}
                  className={styles.filterSelect}
                  value={current}
                  onChange={(e) => {
                    col?.setFilterValue(e.target.value || undefined)
                    setPagination((p) => (p.pageIndex === 0 ? p : { ...p, pageIndex: 0 }))
                  }}
                >
                  <option value="">{f.allLabel}</option>
                  {f.options.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              )
            })}
          </div>
        )}
      </div>

      <div tabIndex={0} role="region" aria-label={label} className={styles.tableWrapper}>
        {rows.length === 0 ? (
          <p className={styles.empty}>{emptyMessage}</p>
        ) : (
          <table className={styles.table}>
            <thead className={styles.thead}>
              {table.getHeaderGroups().map((headerGroup) => (
                <tr key={headerGroup.id}>
                  {headerGroup.headers.map((header) => {
                    const col = columnById.get(header.column.id)
                    const isAction = header.column.id === ACTION_COLUMN_ID
                    const sorted = header.column.getIsSorted()
                    const canSort = header.column.getCanSort()
                    return (
                      <th
                        key={header.id}
                        scope="col"
                        className={styles.headerCell}
                        data-align={isAction ? 'right' : col?.align}
                        aria-sort={
                          canSort ? (sorted === 'asc' ? 'ascending' : sorted === 'desc' ? 'descending' : 'none') : undefined
                        }
                      >
                        {canSort ? (
                          <button
                            type="button"
                            className={`ui-strong ${styles.sortButton}`}
                            onClick={header.column.getToggleSortingHandler()}
                          >
                            <span>{flexRender(header.column.columnDef.header, header.getContext())}</span>
                            <span className={styles.sortIcon} aria-hidden="true">
                              {sorted === 'asc' ? '↑' : sorted === 'desc' ? '↓' : '↕'}
                            </span>
                          </button>
                        ) : (
                          <span className={`ui-strong ${styles.headerText}`}>
                            {flexRender(header.column.columnDef.header, header.getContext())}
                          </span>
                        )}
                      </th>
                    )
                  })}
                </tr>
              ))}
            </thead>
            <tbody className={styles.tbody}>
              {rows.map((row) => (
                <tr key={row.id}>
                  {row.getAllCells().map((cell, index) => {
                    const col = columnById.get(cell.column.id)
                    const isAction = cell.column.id === ACTION_COLUMN_ID
                    if (index === 0) {
                      return (
                        <th key={cell.id} scope="row" className={styles.rowHeader}>
                          {flexRender(cell.column.columnDef.cell, cell.getContext())}
                        </th>
                      )
                    }
                    const headerText = isAction ? actionHeader : (col?.header ?? '')
                    return (
                      <td
                        key={cell.id}
                        className={styles.cell}
                        data-mono={col?.mono || undefined}
                        data-align={col?.align}
                        data-action={isAction || undefined}
                        style={{ '--cell-label': `"${headerText}"` } as CSSProperties}
                      >
                        <span className={styles.cellValue}>
                          {flexRender(cell.column.columnDef.cell, cell.getContext())}
                        </span>
                      </td>
                    )
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className={styles.pagination}>
        <p className={styles.pageCount} aria-live="polite">
          {totalRows === 0 ? '0 results' : `${start}–${end} of ${totalRows}`}
        </p>
        <div className={styles.pageButtons}>
          <button
            type="button"
            className={`ui-strong ${styles.pageButton}`}
            disabled={!table.getCanPreviousPage()}
            onClick={() => table.previousPage()}
          >
            Previous
          </button>
          <button
            type="button"
            className={`ui-strong ${styles.pageButton}`}
            disabled={!table.getCanNextPage()}
            onClick={() => table.nextPage()}
          >
            Next
          </button>
        </div>
      </div>
    </div>
  )
}
