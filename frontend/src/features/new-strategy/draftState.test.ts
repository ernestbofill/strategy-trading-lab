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
  it('keeps everything while the same template stays chosen', () => {
    expect(withTemplate(changed, 'comeback')).toBe(changed)
  })

  it('starts over from the defaults for another template', () => {
    expect(withTemplate(changed, 'trend')).toEqual({ ...initialDraft, templateKey: 'trend' })
  })
})
