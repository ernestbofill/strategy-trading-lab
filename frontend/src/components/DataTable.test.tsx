// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DataTable, type DataTableColumn } from './DataTable'

afterEach(cleanup)

interface Item {
  id: string
  name: string
  symbol: string
  kind: 'stock' | 'fund'
  count: number
}

const sample: Item[] = [
  { id: '1', name: 'Zscaler', symbol: 'ZS', kind: 'stock', count: 3 },
  { id: '2', name: 'Eaton', symbol: 'ETN', kind: 'stock', count: 1 },
  { id: '3', name: 'SPDR S&P 500 ETF', symbol: 'SPY', kind: 'fund', count: 5 },
  { id: '4', name: 'Modine', symbol: 'MOD', kind: 'stock', count: 2 },
  { id: '5', name: 'Invesco QQQ Trust', symbol: 'QQQ', kind: 'fund', count: 4 },
]

const columns: DataTableColumn<Item>[] = [
  { id: 'name', header: 'Name', accessorFn: (r) => r.name },
  { id: 'symbol', header: 'Symbol', accessorFn: (r) => r.symbol, mono: true },
  { id: 'kind', header: 'Kind', accessorFn: (r) => r.kind },
  { id: 'count', header: 'Strategies', accessorFn: (r) => r.count, mono: true, align: 'right' },
]

function rowNames() {
  return screen.getAllByRole('row').slice(1).map((row) => within(row).getByRole('rowheader').textContent)
}

describe('DataTable', () => {
  it('sorts by a column when its header button is clicked', () => {
    render(
      <DataTable
        label="Assets"
        data={sample}
        getRowId={(r) => r.id}
        columns={columns}
        initialSort={{ id: 'name' }}
      />,
    )

    expect(rowNames()).toEqual(['Eaton', 'Invesco QQQ Trust', 'Modine', 'SPDR S&P 500 ETF', 'Zscaler'])
    expect(screen.getByRole('columnheader', { name: 'Name' }).getAttribute('aria-sort')).toBe('ascending')

    fireEvent.click(screen.getByRole('button', { name: 'Name' }))
    expect(rowNames()).toEqual(['Zscaler', 'SPDR S&P 500 ETF', 'Modine', 'Invesco QQQ Trust', 'Eaton'])
    expect(screen.getByRole('columnheader', { name: 'Name' }).getAttribute('aria-sort')).toBe('descending')

    fireEvent.click(screen.getByRole('button', { name: 'Strategies' }))
    expect(rowNames()).toEqual(['Eaton', 'Modine', 'Zscaler', 'Invesco QQQ Trust', 'SPDR S&P 500 ETF'])
  })

  it('searches across columns and filters by dropdown', () => {
    render(
      <DataTable
        label="Assets"
        data={sample}
        getRowId={(r) => r.id}
        columns={columns}
        initialSort={{ id: 'name' }}
        filters={[
          {
            columnId: 'kind',
            label: 'Kind',
            allLabel: 'All kinds',
            options: [
              { value: 'stock', label: 'Stocks' },
              { value: 'fund', label: 'Funds' },
            ],
          },
        ]}
      />,
    )

    fireEvent.change(screen.getByLabelText('Kind'), { target: { value: 'fund' } })
    expect(rowNames()).toEqual(['Invesco QQQ Trust', 'SPDR S&P 500 ETF'])

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search' }), { target: { value: 'spy' } })
    expect(rowNames()).toEqual(['SPDR S&P 500 ETF'])

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search' }), { target: { value: 'nothing-matches' } })
    expect(screen.getByText('No matching rows.')).toBeTruthy()
    expect(screen.getByText('0 results')).toBeTruthy()
  })

  it('pages through rows and runs the per-row action', () => {
    const onAction = vi.fn()
    render(
      <DataTable
        label="Assets"
        data={sample}
        getRowId={(r) => r.id}
        columns={columns}
        initialSort={{ id: 'name' }}
        pageSize={2}
        rowAction={(r) => (
          <button type="button" onClick={() => onAction(r.id)}>
            Hide {r.symbol}
          </button>
        )}
      />,
    )

    expect(screen.getByText('1–2 of 5')).toBeTruthy()
    expect(rowNames()).toEqual(['Eaton', 'Invesco QQQ Trust'])
    expect((screen.getByRole('button', { name: 'Previous' }) as HTMLButtonElement).disabled).toBe(true)

    fireEvent.click(screen.getByRole('button', { name: 'Hide ETN' }))
    expect(onAction).toHaveBeenCalledWith('2')

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    expect(screen.getByText('3–4 of 5')).toBeTruthy()
    expect(rowNames()).toEqual(['Modine', 'SPDR S&P 500 ETF'])

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    expect(screen.getByText('5–5 of 5')).toBeTruthy()
    expect(rowNames()).toEqual(['Zscaler'])
    expect((screen.getByRole('button', { name: 'Next' }) as HTMLButtonElement).disabled).toBe(true)
  })
})
