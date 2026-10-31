// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Account, StrategyTemplate } from '../../api'
import { AccountProvider } from '../account/AccountContext'
import { NewStrategyDraftProvider } from './NewStrategyDraft'
import { PickTemplate } from './PickTemplate'

const templates: StrategyTemplate[] = [
  {
    key: 'dip',
    engineKey: 'dip',
    name: 'Dip in winners',
    tagline: 'Find the next Nvidia',
    idea: 'Pick winning stocks.',
    investsIn: 'stockList',
    signals: 'buysOnly',
    aiRound: null,
  },
]

vi.mock('./useTemplate', () => ({
  useTemplates: () => ({ status: 'ready', data: templates }),
}))

let currentAccount: Account | null = null

vi.mock('../account/AccountContext', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../account/AccountContext')>()
  return {
    ...actual,
    useAccount: () => ({
      state: { status: 'ready', account: currentAccount },
      refresh: vi.fn(),
      owner: currentAccount?.email ?? null,
      clear: vi.fn(),
    }),
    useOptionalAccount: () => currentAccount,
  }
})

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  currentAccount = null
})

function renderPickTemplate() {
  return render(
    <MemoryRouter>
      <AccountProvider>
        <NewStrategyDraftProvider owner={currentAccount?.email ?? null}>
          <PickTemplate />
        </NewStrategyDraftProvider>
      </AccountProvider>
    </MemoryRouter>,
  )
}

describe('PickTemplate', () => {
  it('shows LOG IN top bar link and welcome eyebrow for guests', () => {
    currentAccount = null
    renderPickTemplate()

    expect(screen.getByText('Welcome to Strategy Trading Lab')).toBeTruthy()
    const logInLink = screen.getByRole('link', { name: 'LOG IN' })
    expect(logInLink.getAttribute('href')).toBe('/log-in?next=%2Fnew')
    expect(screen.queryByText('1 / 5')).toBeNull()
  })

  it('shows 1 / 5 step in top bar for logged-in users', () => {
      email: 'user@example.com',
      firstName: null,
      lastName: null,
      email: 'user@example.com',
      emailVerified: true,
      locale: 'en',
      timeZone: 'UTC',
      overviewEmail: true,
      currency: 'EUR',
      role: 'user',
      aiSharing: false,
    }
    renderPickTemplate()

    expect(screen.getByText('1 / 5')).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'LOG IN' })).toBeNull()
  })
})
