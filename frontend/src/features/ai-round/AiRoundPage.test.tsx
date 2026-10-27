// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, type AiReading, type AiRoundDetail } from '../../api'
import { invalidateAll } from '../../lib/useLoadable'
import { AiRoundPage } from './AiRoundPage'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
  invalidateAll()
})

const round = (fields: Partial<AiRoundDetail> = {}): AiRoundDetail => ({
  strategyId: 'st',
  strategyName: 'My AI picks',
  templateName: "Your AI's top picks",
  mode: 'simulation',
  currency: 'EUR',
  roundId: 'r1',
  dueOn: '2026-10-27',
  lastDay: '2026-11-02',
  open: true,
  today: '2026-10-27',
  promptVersion: 1,
  promptEdited: false,
  prompt: 'Today is October 27, 2026.\n\nScore each stock\n- News: …\n\n| Stock | News | Overall | Reason |\n\nStocks:\n- ZS: Zscaler',
  setup: { news: true, mood: false, figures: false, instructions: null },
  suggested: 'Score each stock',
  stocks: [
    { assetId: 'zs', symbol: 'ZS', name: 'Zscaler' },
    { assetId: 'vrt', symbol: 'VRT', name: 'Vertiv' },
  ],
  everyWeeks: 2,
  topN: 1,
  lastModel: 'Claude · Opus',
  sharing: false,
  sharingAsked: false,
  ...fields,
})

const reading: AiReading = {
  scores: [
    { assetId: 'zs', symbol: 'ZS', name: 'Zscaler', overall: 83, news: 82, mood: null, figures: null, reason: 'Strong quarter', step: 2 },
    { assetId: 'vrt', symbol: 'VRT', name: 'Vertiv', overall: 61, news: 60, mood: null, figures: null, reason: 'Quiet', step: null },
  ],
  problems: [],
  ok: true,
  maxSteps: 5,
  stepAmount: 100,
  short: null,
}

function open(detail: AiRoundDetail) {
  vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
  vi.spyOn(api, 'getAiRound').mockResolvedValue(detail)
  render(
    <MemoryRouter initialEntries={['/strategies/st/ai-round']}>
      <Routes>
        <Route path="/strategies/:id/ai-round" element={<AiRoundPage />} />
        <Route path="/strategies/:id/signals/:signalId" element={<p>Signal page</p>} />
        <Route path="/" element={<p>Dashboard</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

const paste = (text: string) => fireEvent.change(screen.getByLabelText("Your AI's whole answer"), { target: { value: text } })

describe('AiRoundPage', () => {
  it('reads the pasted answer, shows the scores and the step, and uses them with the sharing choice', async () => {
    open(round())
    const read = vi.spyOn(api, 'readAiAnswer').mockResolvedValue(reading)
    const use = vi.spyOn(api, 'useAiScores').mockResolvedValue({ roundId: 'r1', signalIds: ['sig'], mode: 'simulation' })

    expect(await screen.findByRole('heading', { name: 'Time for the AI round' })).toBeTruthy()
    expect((screen.getByLabelText('AI model you used') as HTMLSelectElement).value).toBe('Claude · Opus')
    expect(screen.getByText('Use these scores').getAttribute('aria-disabled')).toBe('true')

    paste('| Stock | Overall |\n| ZS | 83 |')
    await screen.findByText('✓ Every stock scored, all scores 0-100.')
    expect(read).toHaveBeenCalledWith('st', '| Stock | Overall |\n| ZS | 83 |')
    expect(screen.getByText('Zscaler gets step 2 of 5 (€100).')).toBeTruthy()
    expect(screen.getByText('Strong quarter')).toBeTruthy()

    fireEvent.click(screen.getByRole('checkbox', { name: 'Share my AI rounds' }))
    fireEvent.click(screen.getByRole('button', { name: 'Use these scores' }))

    await screen.findByText('Dashboard')
    expect(use).toHaveBeenCalledWith('st', '| Stock | Overall |\n| ZS | 83 |', 'Claude · Opus', true, round().prompt)
  })

  it('waits for a new answer to be read before its scores can be used', async () => {
    open(round())
    let finish: (reading: AiReading) => void = () => {}
    vi.spyOn(api, 'readAiAnswer')
      .mockResolvedValueOnce(reading)
      .mockImplementationOnce(() => new Promise((resolve) => (finish = resolve)))

    await screen.findByRole('heading', { name: 'Time for the AI round' })
    paste('first answer')
    await screen.findByText('✓ Every stock scored, all scores 0-100.')
    expect(screen.getByRole('button', { name: 'Use these scores' })).toBeTruthy()

    paste('second answer')
    expect(screen.getByText('Use these scores').getAttribute('aria-disabled')).toBe('true')
    await waitFor(() => expect(api.readAiAnswer).toHaveBeenCalledTimes(2))
    finish(reading)
    expect(await screen.findByRole('button', { name: 'Use these scores' })).toBeTruthy()
  })

  it('points out what the AI should answer again, and keeps the button off', async () => {
    open(round({ sharingAsked: true, sharing: true }))
    vi.spyOn(api, 'readAiAnswer').mockResolvedValue({ ...reading, ok: false, problems: ['Vertiv has no score.'], scores: reading.scores.slice(0, 1) })

    await screen.findByRole('heading', { name: 'Time for the AI round' })
    expect(screen.getByText(/Sharing your AI rounds/)).toBeTruthy()
    expect(screen.queryByRole('checkbox', { name: 'Share my AI rounds' })).toBeNull()
    paste('something')

    expect(await screen.findByText('Vertiv has no score.')).toBeTruthy()
    expect(screen.getByText('Use these scores').getAttribute('aria-disabled')).toBe('true')
  })

  it('opens Confirm a signal after a live round, and asks for the model when it is Other', async () => {
    open(round({ mode: 'live', lastModel: 'Mistral Large', sharingAsked: true }))
    vi.spyOn(api, 'readAiAnswer').mockResolvedValue(reading)
    const use = vi.spyOn(api, 'useAiScores').mockResolvedValue({ roundId: 'r1', signalIds: ['sig'], mode: 'live' })

    await screen.findByRole('heading', { name: 'Time for the AI round' })
    expect((screen.getByLabelText('Which AI model?') as HTMLInputElement).value).toBe('Mistral Large')
    paste('answer')
    await screen.findByText('✓ Every stock scored, all scores 0-100.')
    fireEvent.click(screen.getByRole('button', { name: 'Use these scores' }))

    await screen.findByText('Signal page')
    expect(use).toHaveBeenCalledWith('st', 'answer', 'Mistral Large', null, round().prompt)
  })

  it('says when the next round is due before it opens', async () => {
    open(round({ open: false, dueOn: '2026-11-10', lastDay: '2026-11-16' }))

    expect(await screen.findByRole('heading', { name: 'Next AI round' })).toBeTruthy()
    expect(screen.getByText(/Your next AI round is due on/)).toBeTruthy()
    await waitFor(() => expect(screen.queryByLabelText("Your AI's whole answer")).toBeNull())
  })
})
