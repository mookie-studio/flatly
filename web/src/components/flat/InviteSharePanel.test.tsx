import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { HttpResponse, http } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { server } from '@/test/server'
import { config } from '@/lib/config'
import { InviteSharePanel } from './InviteSharePanel'

const inviteUrl = `${config.apiUrl}/api/v1/flats/f1/invite-code`
const regenerateUrl = `${config.apiUrl}/api/v1/flats/f1/invite-code/regenerate`

function renderPanel() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <InviteSharePanel flatId="f1" />
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('invite sharing', () => {
  it('shows the code with its expiry and a WhatsApp share link', async () => {
    server.use(
      http.get(inviteUrl, () =>
        HttpResponse.json({ code: 'ABCD2345', expiresAt: '2026-11-27T00:00:00Z' }),
      ),
    )
    renderPanel()

    expect(await screen.findByText('ABCD2345')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Share on WhatsApp' })).toHaveAttribute(
      'href',
      expect.stringContaining('https://wa.me/?text='),
    )
  })

  it('builds the WhatsApp message around the join link', async () => {
    server.use(
      http.get(inviteUrl, () =>
        HttpResponse.json({ code: 'ABCD2345', expiresAt: '2026-11-27T00:00:00Z' }),
      ),
    )
    renderPanel()

    const link = await screen.findByRole('link', { name: 'Share on WhatsApp' })
    const text = new URL(link.getAttribute('href') ?? '').searchParams.get('text')
    expect(text).toBe(`Join our flat on Flatly: ${window.location.origin}/join?code=ABCD2345`)
  })

  it('copies the join link to the clipboard', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText } })
    server.use(
      http.get(inviteUrl, () =>
        HttpResponse.json({ code: 'ABCD2345', expiresAt: '2026-11-27T00:00:00Z' }),
      ),
    )
    renderPanel()

    fireEvent.click(await screen.findByRole('button', { name: 'Copy link' }))

    expect(writeText).toHaveBeenCalledWith(`${window.location.origin}/join?code=ABCD2345`)
    expect(await screen.findByText('Link copied.')).toBeInTheDocument()
  })

  it('hides the sharing sheet where the browser has none', async () => {
    vi.stubGlobal('navigator', { ...navigator, share: undefined })
    server.use(
      http.get(inviteUrl, () =>
        HttpResponse.json({ code: 'ABCD2345', expiresAt: '2026-11-27T00:00:00Z' }),
      ),
    )
    renderPanel()

    await screen.findByText('ABCD2345')
    expect(screen.queryByRole('button', { name: 'Share' })).not.toBeInTheDocument()
  })

  it('replaces the code after confirmation and no longer shows the old one', async () => {
    server.use(
      http.get(inviteUrl, () =>
        HttpResponse.json({ code: 'OLDCODE2', expiresAt: '2026-11-27T00:00:00Z' }),
      ),
      http.post(regenerateUrl, () =>
        HttpResponse.json({ code: 'NEWCODE3', expiresAt: '2026-11-27T00:00:00Z' }),
      ),
    )
    renderPanel()

    fireEvent.click(await screen.findByRole('button', { name: 'Create a new code' }))
    const dialog = screen.getByRole('alertdialog')
    expect(within(dialog).getByText('The current code and link will stop working.')).toBeVisible()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Create new code' }))

    expect(await screen.findByText('NEWCODE3')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByText('OLDCODE2')).not.toBeInTheDocument())
  })
})
