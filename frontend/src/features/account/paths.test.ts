import { describe, expect, it } from 'vitest'
import { dashboardPath } from '../new-strategy/paths'
import { logInPathFor, safeNext } from './paths'

describe('safeNext', () => {
  it('keeps a path inside the app, with its query', () => {
    expect(safeNext('/new/budget?x=1')).toBe('/new/budget?x=1')
  })

  it('goes to the dashboard for anything else', () => {
    for (const next of [null, undefined, '', 'dashboard', '//evil.com', '/\\evil.com', 'https://evil.com', 'javascript:alert(1)'])
      expect(safeNext(next)).toBe(dashboardPath)
  })

  it('round-trips through the log-in link', () => {
    const from = '/strategies/1?tab=a&b=c'
    expect(safeNext(new URLSearchParams(logInPathFor(from).split('?')[1]).get('next'))).toBe(from)
  })
})
