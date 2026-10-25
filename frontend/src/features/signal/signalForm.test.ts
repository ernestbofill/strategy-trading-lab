import { describe, expect, it } from 'vitest'
import type { SignalDetail } from '../../api'
import { tNow } from '../../i18n/I18n'
import { ambiguousFields, initialForm, isAmbiguous, nextStep, parseNumber, rateOn, tradeRequest } from './signalForm'

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
  it('starts from the next day, its open and the step without the fee, or held shares for a sale', () => {
    expect(initialForm(signal())).toMatchObject({ date: '2026-10-23', price: '190', amount: '98.80', fee: '', ownRate: null })
    expect(initialForm(signal({ prefill: { date: '2026-10-23', price: null, amount: null } }))).toMatchObject({ price: '', amount: '' })
    expect(initialForm(signal({ side: 'sell', prefill: { date: '2026-10-23', price: 190, amount: null, shares: 5.25 } }))).toMatchObject({
      enteredAs: 'shares',
      shares: '5.25',
    })
  })

  it('reads numbers with a dot or a comma', () => {
    expect([parseNumber('98.80'), parseNumber('98,80'), parseNumber('1,234.5'), parseNumber(''), parseNumber('abc')]).toEqual([98.8, 98.8, 1234.5, null, null])
    expect([parseNumber('12,345,678'), parseNumber('1,2'), parseNumber('1,2500'), parseNumber('0,125'), parseNumber('1.250,50')]).toEqual([
      12345678, 1.2, 1.25, 0.125, 1250.5,
    ])
  })

  it('asks rather than guess at a number that reads two ways', () => {
    expect([parseNumber('1,250'), isAmbiguous('1,250'), isAmbiguous('0,125'), isAmbiguous('1,25'), isAmbiguous('1.250')]).toEqual([null, true, false, false, true])
    expect(Object.keys(ambiguousFields({ ...initialForm(signal()), price: '1,250', fee: '1,20' }, 'unclear'))).toEqual(['price'])
  })

  it('uses the latest rate on or before the day', () => {
    expect(rateOn(signal().rates, '2026-10-25')?.rate).toBe(1.09)
    expect(rateOn(signal().rates, '2026-10-21')).toBeNull()
  })

  it('sends an emptied date as null, so the server names the field', () => {
    expect(tradeRequest({ ...initialForm(signal()), date: '' }).date).toBeNull()
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
    expect(nextStep(signal(), tNow)).toBe('step 3 of 5')
    expect(nextStep(signal({ stepNo: 5 }), tNow)).toBeNull()
  })
})
