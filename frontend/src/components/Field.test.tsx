// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { SegmentedField, SelectField } from './Field'

afterEach(cleanup)

const options = [{ value: 'EUR', label: 'EUR · Euro' }]

describe('SelectField', () => {
  it('shows its label by default', () => {
    render(<SelectField label="Currency" value="EUR" onChange={() => {}} options={options} />)
    expect(screen.getByText('Currency').className).not.toContain('visually-hidden')
    expect(screen.getByRole('combobox', { name: 'Currency' })).toBeDefined()
  })

  it('keeps a hidden label for screen readers', () => {
    render(<SelectField label="Currency" hideLabel value="EUR" onChange={() => {}} options={options} />)
    expect(screen.getByText('Currency').className).toContain('visually-hidden')
    expect(screen.getByRole('combobox', { name: 'Currency' })).toBeDefined()
  })
})

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
