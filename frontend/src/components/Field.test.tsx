// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { SelectField } from './Field'

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
