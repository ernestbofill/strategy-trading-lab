import { describe, expect, it } from 'vitest'
import type { SignalDetail } from '../../api'
import { initialForm, nextStep, parseNumber, rateOn, tradeRequest } from './signalForm'

const signal = (fields: Partial<SignalDetail> = {}): SignalDetail => ({
  id: 's',
  strategyId: 'st',
  strategyName: 'Zscaler & friends',
  templateName: 'Dip in winners',
  strategyCurrency: 'EUR',
  stockName: 'Zscaler',
  assetCurrency: 'USD',
  stepNo: 2,
  maxSteps: 5,
  stepAmount: 100,
  signalDate: '2026-10-22',
  closePrice: 187.5,
  reason: '8% below its recent peak',
  status: 'waiting',
  snoozedUntil: null,
  endedOn: null,
  today: '2026-10-24',
  prefill: { date: '2026-10-23', price: 190, amount: 98.8 },
  rates: [
    { date: '2026-10-22', rate: 1.08 },
    { date: '2026-10-23', rate: 1.09 },
  ],
  answer: null,
  ...fields,
})

describe('signalForm', () => {
  it('starts from the next day, its open and the step without the fee', () => {
    expect(initialForm(signal())).toMatchObject({ date: '2026-10-23', price: '190', amount: '98.80', fee: '', ownRate: null })
    expect(initialForm(signal({ prefill: { date: '2026-10-23', price: null, amount: null } }))).toMatchObject({ price: '', amount: '' })
  })

  it('reads numbers with a dot or a comma', () => {
    expect([parseNumber('98.80'), parseNumber('98,80'), parseNumber('1,234.5'), parseNumber(''), parseNumber('abc')]).toEqual([98.8, 98.8, 1234.5, null, null])
  })

  it('uses the latest rate on or before the day', () => {
    expect(rateOn(signal().rates, '2026-10-25')?.rate).toBe(1.09)
    expect(rateOn(signal().rates, '2026-10-21')).toBeNull()
  })

  it('sends what was typed, with the reference rate unless the user gave their own', () => {
    const form = { ...initialForm(signal()), fee: '1,20' }
    expect(tradeRequest(form)).toEqual({
      date: '2026-10-23', price: 190, enteredAs: 'amount', amount: 98.8, shares: null, fee: 1.2, fxSource: 'reference', fxRate: null,
    })
    expect(tradeRequest({ ...form, enteredAs: 'shares', shares: '0.5', ownRate: '1.07' })).toMatchObject({
      enteredAs: 'shares', amount: null, shares: 0.5, fxSource: 'user', fxRate: 1.07,
    })
  })

  it('names the next step after a skip until the last one', () => {
    expect(nextStep(signal())).toBe('step 3 of 5')
    expect(nextStep(signal({ stepNo: 5 }))).toBeNull()
  })
})
