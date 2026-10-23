import { describe, expect, it } from 'vitest'
import { NameTakenError } from '../errors'
import type { NewStrategy } from '../types'
import { mockApi } from './mockApi'

const strategy = (name: string): NewStrategy => ({
  templateKey: 'dip',
  name,
  mode: 'simulation',
  currency: 'EUR',
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

describe('mockApi database', () => {
  it('switches, and the version follows', async () => {
    await mockApi.adminLogIn('STL')

    await mockApi.switchAdminDatabase('production')

    expect((await mockApi.getVersion()).database).toBe('production')
    expect((await mockApi.getAdminDatabases()).active).toBe('production')
    await mockApi.switchAdminDatabase('dev')
    expect((await mockApi.getVersion()).database).toBe('dev')
  })

  it('checks the price schedule, tests Yahoo and refuses a second refresh while one runs', async () => {
    await mockApi.adminLogIn('STL')

    await expect(mockApi.setAdminSetting('PRICES_DAILY_TIME', '7:30')).rejects.toThrow('Use HH:MM')
    await expect(mockApi.setAdminSetting('PRICES_TIME_ZONE', 'Mars/Base')).rejects.toThrow('time zone')
    await mockApi.setAdminSetting('PRICES_DAILY_TIME', '22:45')
    expect((await mockApi.testAdminYahoo()).every((c) => c.ok)).toBe(true)
    await mockApi.refreshAdminPrices()
    expect((await mockApi.getAdminPrices()).running).toBe(true)
    await expect(mockApi.refreshAdminPrices()).rejects.toThrow('already running')
  })
})
