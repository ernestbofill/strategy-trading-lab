// @vitest-environment jsdom
import { afterEach, describe, expect, it } from 'vitest'
import { currentUiZoom, setUiZoom, storedUiZoom, uiZoom } from './uiZoom'

afterEach(() => {
  setUiZoom('1')
  localStorage.clear()
})

describe('uiZoom', () => {
  it('falls back to 100% for an unknown stored value', () => {
    localStorage.setItem('stl.zoom', '3')
    expect(storedUiZoom()).toBe('1')
  })

  it('zooms the page, keeps the choice, and clears it at 100%', () => {
    setUiZoom('1.25')
    expect(storedUiZoom()).toBe('1.25')
    expect(currentUiZoom()).toBe('1.25')
    expect(uiZoom()).toBe(1.25)
    expect(document.documentElement.style.getPropertyValue('--ui-zoom')).toBe('1.25')
    setUiZoom('1')
    expect(document.documentElement.style.getPropertyValue('--ui-zoom')).toBe('')
  })
})
