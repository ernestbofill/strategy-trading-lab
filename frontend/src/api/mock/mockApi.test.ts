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
