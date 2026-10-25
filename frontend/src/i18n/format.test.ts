import { describe, expect, it } from 'vitest'
import { formatMessage } from './format'

const days = '{n, plural, one {# day} other {# days}}'

describe('formatMessage', () => {
  it('returns a message without braces or params as it is', () => {
    expect(formatMessage('Save', undefined, 'en')).toBe('Save')
    expect(formatMessage('{name}', undefined, 'en')).toBe('{name}')
  })

  it('fills {name} params, strings and numbers', () => {
    expect(formatMessage('Hello {name}, you have {n} signals.', { name: 'Ana', n: 3 }, 'en')).toBe('Hello Ana, you have 3 signals.')
    expect(formatMessage('{a}{a}', { a: 'x' }, 'en')).toBe('xx')
  })

  it('keeps a placeholder whose param is missing', () => {
    expect(formatMessage('Hi {name}.', { other: 1 }, 'en')).toBe('Hi {name}.')
  })

  it('picks the plural form: one and other, # is the number', () => {
    expect(formatMessage(days, { n: 1 }, 'en')).toBe('1 day')
    expect(formatMessage(days, { n: 0 }, 'en')).toBe('0 days')
    expect(formatMessage(days, { n: 5 }, 'en')).toBe('5 days')
  })

  it('works around a plural inside a sentence, and with other params', () => {
    expect(formatMessage('Next in {n, plural, one {# day} other {# days}} ({who}).', { n: 2, who: 'Ana' }, 'en')).toBe(
      'Next in 2 days (Ana).',
    )
  })

  it('handles braces nested in a plural form', () => {
    const message = '{n, plural, one {{name} has # step} other {{name} has # steps}}'
    expect(formatMessage(message, { n: 1, name: 'Ana' }, 'en')).toBe('Ana has 1 step')
    expect(formatMessage(message, { n: 4, name: 'Ana' }, 'en')).toBe('Ana has 4 steps')
  })

  it('prefers an exact =n form', () => {
    const message = '{n, plural, =0 {none} one {# day} other {# days}}'
    expect(formatMessage(message, { n: 0 }, 'en')).toBe('none')
    expect(formatMessage(message, { n: 1 }, 'en')).toBe('1 day')
  })

  it('uses Catalan plural rules', () => {
    const ca = '{n, plural, one {# dia} other {# dies}}'
    expect(formatMessage(ca, { n: 1 }, 'ca')).toBe('1 dia')
    expect(formatMessage(ca, { n: 3 }, 'ca')).toBe('3 dies')
    expect(formatMessage('Hola {name}.', { name: 'Anna' }, 'ca')).toBe('Hola Anna.')
  })
})
