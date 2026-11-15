// @vitest-environment jsdom
import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { SegmentedField } from './Field'

describe('SegmentedField', () => {
  const currencies = [
    { value: 'EUR', label: '€ EUR', detail: 'Euro' },
    { value: 'USD', label: '$ USD', detail: 'US dollar' },
  ]

  it('is a radio group named by its label, with the value checked', () => {
    render(<SegmentedField label="Currency" hideLabel value="EUR" onChange={() => {}} options={currencies} />)
    expect(screen.getByRole('group', { name: 'Currency' })).toBeDefined()
    expect((screen.getByRole('radio', { name: '€ EUR Euro' }) as HTMLInputElement).checked).toBe(true)
    expect((screen.getByRole('radio', { name: '$ USD US dollar' }) as HTMLInputElement).checked).toBe(false)
  })

  it('reports the picked value', () => {
    const onChange = vi.fn()
    render(<SegmentedField label="Currency" value="EUR" onChange={onChange} options={currencies} />)
    fireEvent.click(screen.getByRole('radio', { name: '$ USD US dollar' }))
    expect(onChange).toHaveBeenCalledWith('USD')
  })
})
