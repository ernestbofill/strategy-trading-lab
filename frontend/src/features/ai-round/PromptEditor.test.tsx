// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../api'
import { PromptEditor } from './PromptEditor'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

function open(instructions: string | null, onSave = vi.fn().mockResolvedValue(undefined)) {
  vi.spyOn(api, 'getVersion').mockResolvedValue({ version: '1', commit: null, builtAt: null, database: 'production' })
  const preview = vi.spyOn(api, 'previewAiPrompt').mockImplementation(async (request) => ({
    version: 1,
    text: `Today is …\n\n${request.instructions ?? 'Suggested words'}\n\n| Stock | Overall | Reason |`,
    suggested: 'Suggested words',
  }))
  render(
    <MemoryRouter>
      <PromptEditor
        top={null}
        initial={{ news: true, mood: false, figures: false, instructions }}
        everyWeeks={2}
        assetIds={['zs']}
        onSave={onSave}
      />
    </MemoryRouter>,
  )
  return { onSave, preview }
}

describe('PromptEditor', () => {
  it('starts from the suggested words and saves them as not edited', async () => {
    const { onSave } = open(null)

    expect(((await screen.findByDisplayValue('Suggested words')) as HTMLTextAreaElement).value).toBe('Suggested words')
    expect(screen.getByRole('button', { name: '⟳ Reset to suggested' }).hasAttribute('disabled')).toBe(true)
    fireEvent.click(screen.getByRole('button', { name: 'Save prompt' }))

    expect(onSave).toHaveBeenCalledWith({ news: true, mood: false, figures: false, instructions: null })
  })

  it("saves the user's own words, and Reset to suggested puts the suggested ones back", async () => {
    const { onSave } = open(null)
    const field = await screen.findByLabelText('Instructions')
    await screen.findByDisplayValue('Suggested words')

    fireEvent.change(field, { target: { value: 'Score the AI products.' } })
    expect(await screen.findByText('// your prompt · v1')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Save prompt' }))
    expect(onSave).toHaveBeenLastCalledWith({ news: true, mood: false, figures: false, instructions: 'Score the AI products.' })

    fireEvent.click(screen.getByRole('button', { name: '⟳ Reset to suggested' }))
    expect((field as HTMLTextAreaElement).value).toBe('Suggested words')
  })

  it('keeps the last source on', async () => {
    open('Mine')
    await screen.findByDisplayValue('Mine')
    const news = screen.getByRole('button', { name: /News/ })

    fireEvent.click(news)

    expect(news.getAttribute('aria-pressed')).toBe('true')
  })
})
