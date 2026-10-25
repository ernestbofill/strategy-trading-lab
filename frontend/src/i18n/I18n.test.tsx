// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { I18nProvider, tNow, useI18n, useT } from './I18n'
import { currentLocale, setCurrentLocale } from './locale'
import { ca } from './messages/ca'
import { en } from './messages/en'

const invalidateAll = vi.hoisted(() => vi.fn())
vi.mock('../lib/useLoadable', () => ({ invalidateAll }))

function Probe() {
  const t = useT()
  const { setLocale } = useI18n()
  return (
    <>
      <p>{t('common.back')}</p>
      <button onClick={() => setLocale('en')}>to-en</button>
      <button onClick={() => setLocale('ca')}>to-ca</button>
    </>
  )
}

const choose = (locale: 'en' | 'ca') => fireEvent.click(screen.getByText('to-' + locale))

const renderProbe = () =>
  render(
    <I18nProvider>
      <Probe />
    </I18nProvider>,
  )

beforeEach(() => {
  localStorage.clear()
  invalidateAll.mockClear()
  setCurrentLocale('en')
})
afterEach(cleanup)

describe('I18nProvider', () => {
  it('speaks English by default', () => {
    renderProbe()
    expect(screen.getByText(en['common.back'])).toBeDefined()
    expect(currentLocale()).toBe('en')
    expect(document.documentElement.lang).toBe('en')
  })

  it('switches the text, stores the choice and sets the page language', () => {
    renderProbe()
    choose('ca')
    expect(screen.getByText(ca['common.back'])).toBeDefined()
    expect(localStorage.getItem('stl.locale')).toBe('ca')
    expect(document.documentElement.lang).toBe('ca')
  })

  it('makes tNow follow the language', () => {
    renderProbe()
    expect(tNow('common.show')).toBe(en['common.show'])
    choose('ca')
    expect(tNow('common.show')).toBe(ca['common.show'])
  })

  it('forgets loaded data when the language changes, but not when it stays', () => {
    renderProbe()
    choose('en')
    expect(invalidateAll).not.toHaveBeenCalled()
    choose('ca')
    expect(invalidateAll).toHaveBeenCalledTimes(1)
  })

  it('starts in the stored language', () => {
    localStorage.setItem('stl.locale', 'ca')
    renderProbe()
    expect(screen.getByText(ca['common.back'])).toBeDefined()
  })

  it('falls back to English when the stored value is unknown', () => {
    localStorage.setItem('stl.locale', 'fr')
    renderProbe()
    expect(screen.getByText(en['common.back'])).toBeDefined()
    expect(currentLocale()).toBe('en')
  })
})
