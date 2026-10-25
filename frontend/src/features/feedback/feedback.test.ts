// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import type { FeedbackItem } from '../../api'
import { buttonSide, elementPath, feedbackMarkdown, isAppPath, topOffset } from './feedback'

const item = (overrides: Partial<FeedbackItem> = {}): FeedbackItem => ({
  id: '019a0000-0000-7000-8000-000000000001',
  userId: '019a0000-0000-7000-8000-000000000002',
  userEmail: 'reader@example.com',
  kind: 'bug',
  status: 'open',
  text: 'Price looks stale',
  route: '/strategies/abc',
  element: 'main > p:nth-of-type(1)',
  selectedText: 'Last close $142.10',
  closedAt: null,
  createdAt: '2026-10-25T10:00:00Z',
  updatedAt: '2026-10-25T10:00:00Z',
  ...overrides,
})

describe('feedbackMarkdown', () => {
  it('formats open items as a Markdown table and skips closed ones', () => {
    const md = feedbackMarkdown([
      item(),
      item({ id: '2', status: 'closed', text: 'Already fixed', closedAt: '2026-10-25T11:00:00Z' }),
      item({
        id: '3',
        userEmail: 'second@example.com',
        kind: 'idea',
        route: null,
        element: null,
        selectedText: null,
        text: 'Line 1\nLine 2 | with pipe\\slash',
      }),
    ])

    expect(md).toBe(
      [
        '# Open feedback (2)',
        '',
        '| # | Kind | User | Page | Element | Selected text | Comment |',
        '| - | ---- | ---- | ---- | ------- | ------------- | ------- |',
        '| 1 | bug | reader@example.com | `/strategies/abc` | `main > p:nth-of-type(1)` | "Last close $142.10" | Price looks stale |',
        '| 2 | idea | second@example.com | — | — | — | Line 1 Line 2 \\| with pipe\\\\slash |',
      ].join('\n'),
    )
  })

  it('returns an empty-state message when no open items exist', () => {
    expect(feedbackMarkdown([item({ status: 'closed' })])).toBe('No open feedback items.')
  })
})

describe('elementPath', () => {
  it('builds a readable DOM selector stopping at main or id', () => {
    document.body.innerHTML = `
      <main>
        <section class="card _cssModuleHash">
          <p>First</p>
          <p>Second</p>
        </section>
      </main>
    `
    const secondP = document.querySelectorAll('p')[1]
    expect(elementPath(secondP.firstChild)).toBe('main > section.card > p:nth-of-type(2)')
  })

  it('stops at an element with an id', () => {
    document.body.innerHTML = `<div id="signals"><p>Row</p></div>`
    const p = document.querySelector('p')!
    expect(elementPath(p)).toBe('div#signals > p')
  })
})

describe('buttonSide and topOffset', () => {
  it('places the button at the bottom when the selection is away from the bottom edge', () => {
    expect(buttonSide({ top: 120, bottom: 160, left: 20, width: 100 }, 800, 48)).toBe('bottom')
  })

  it('moves the button below the header when the selection is near the bottom of the screen', () => {
    expect(buttonSide({ top: 700, bottom: 740, left: 20, width: 100 }, 800, 48)).toBe('top')
    expect(topOffset(48)).toBe(56)
  })
})

describe('isAppPath', () => {
  it('accepts only single-slash relative paths', () => {
    expect(isAppPath('/dashboard')).toBe(true)
    expect(isAppPath('/strategies/123?tab=signals')).toBe(true)
    expect(isAppPath('//evil.example')).toBe(false)
    expect(isAppPath('https://evil.example')).toBe(false)
    expect(isAppPath(null)).toBe(false)
  })
})
