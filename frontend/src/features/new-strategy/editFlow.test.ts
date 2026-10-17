import { describe, expect, it } from 'vitest'
import { editFromConfirm, flowTarget, readEditSource, reviewSubPageBack, subPageLink } from './editFlow'
import { confirmPath, modePath, reviewPath } from './paths'

describe('readEditSource', () => {
  it('reads the source once the confirm screen has been reached', () => {
    expect(readEditSource('?from=confirm', true)).toBe('confirm')
    expect(readEditSource('?from=review', true)).toBe('review')
  })

  it('ignores the link before the confirm screen has been reached', () => {
    expect(readEditSource('?from=confirm', false)).toBeNull()
  })

  it('ignores a missing or unknown source', () => {
    expect(readEditSource('', true)).toBeNull()
    expect(readEditSource('?from=elsewhere', true)).toBeNull()
  })

  it('reads what editFromConfirm writes', () => {
    expect(readEditSource(new URL(editFromConfirm(modePath), 'http://x').search, true)).toBe('confirm')
  })
})

describe('flowTarget', () => {
  it('returns to the confirm screen only when editing from it', () => {
    expect(flowTarget('confirm', modePath)).toBe(confirmPath)
    expect(flowTarget('review', modePath)).toBe(modePath)
    expect(flowTarget(null, modePath)).toBe(modePath)
  })
})

describe('review sub-pages', () => {
  it('keeps the edit going from a review that is being edited', () => {
    expect(subPageLink('confirm', '/x')).toBe('/x?from=review')
    expect(subPageLink(null, '/x')).toBe('/x')
  })

  it('goes back to where the user came from', () => {
    expect(reviewSubPageBack('confirm', 'dip')).toBe(confirmPath)
    expect(reviewSubPageBack('review', 'dip')).toBe(editFromConfirm(reviewPath('dip')))
    expect(reviewSubPageBack(null, 'dip')).toBe(reviewPath('dip'))
  })
})
