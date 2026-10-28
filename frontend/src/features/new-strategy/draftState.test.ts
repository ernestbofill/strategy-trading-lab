import { describe, expect, it } from 'vitest'
import { initialDraft, withTemplate, type DraftState } from './draftState'

const changed: DraftState = {
  ...initialDraft,
  templateKey: 'comeback',
  unticked: ['NVDA'],
  mode: 'live',
  budget: 6000,
  name: 'My strategy',
}

describe('withTemplate', () => {
  it('starts over after a Go live copy, even on the same template', () => {
    const copy: DraftState = { ...initialDraft, templateKey: 'dip', mode: 'live', name: 'Mine live', sourceStrategyId: 's1', currency: 'USD' }
    expect(withTemplate(copy, 'dip')).toEqual({ ...initialDraft, templateKey: 'dip' })
  })

  it('keeps everything while the same template stays chosen', () => {
    expect(withTemplate(changed, 'comeback')).toBe(changed)
  })

  it('starts over from the defaults for another template', () => {
    expect(withTemplate(changed, 'trend')).toEqual({ ...initialDraft, templateKey: 'trend' })
  })
})
