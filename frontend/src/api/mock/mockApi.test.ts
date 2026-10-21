import { describe, expect, it } from 'vitest'
import { NameTakenError } from '../errors'
import type { NewStrategy } from '../types'
import { mockApi } from './mockApi'

const strategy = (name: string): NewStrategy => ({
  templateKey: 'dip',
  name,
  mode: 'simulation',
  budget: { amount: 3000, step: 100, maxSteps: 5 },
  assetIds: [],
})

describe('mockApi.startStrategy', () => {
  it('adds the strategy to the top of the dashboard, started today with nothing used', async () => {
    const created = await mockApi.startStrategy(strategy('  A new one '))
    const { today, strategies } = await mockApi.getDashboard()

    expect(created).toMatchObject({ name: 'A new one', templateName: 'Dip in winners', used: 0, results: null, startedOn: today })
    expect(strategies[0]).toEqual(created)
  })

  it('refuses a name a running strategy has, ignoring case', async () => {
    await expect(mockApi.startStrategy(strategy('ZSCALER & friends'))).rejects.toBeInstanceOf(NameTakenError)
  })
})

describe('mockApi admin', () => {
  it('clears the password when the mail server changes, as the API does', async () => {
    await mockApi.adminLogIn('STL')
    const shown = Object.fromEntries((await mockApi.getAdminSettings()).map((s) => [s.key, s.value]))
    await mockApi.setAdminSetting('SMTP_PASSWORD', 'app password', shown)
    await mockApi.setAdminSetting('SMTP_HOST', 'smtp.gmail.com')
    expect((await mockApi.getAdminSettings()).find((s) => s.key === 'SMTP_PASSWORD')?.isSet).toBe(true)

    await mockApi.setAdminSetting('SMTP_HOST', 'smtp.example.com')
    expect((await mockApi.getAdminSettings()).find((s) => s.key === 'SMTP_PASSWORD')?.isSet).toBe(false)
  })

  it('refuses a password typed on a page showing another mail server', async () => {
    await mockApi.adminLogIn('STL')
    const shown = Object.fromEntries((await mockApi.getAdminSettings()).map((s) => [s.key, s.value]))
    await mockApi.setAdminSetting('SMTP_HOST', 'smtp.other.example')
    await expect(mockApi.setAdminSetting('SMTP_PASSWORD', 'x', shown)).rejects.toThrow('changed since this page loaded')
  })

  it('refuses a port out of range', async () => {
    await mockApi.adminLogIn('STL')
    await expect(mockApi.setAdminSetting('SMTP_PORT', '0')).rejects.toThrow('Use a port from 1 to 65535.')
  })
})
