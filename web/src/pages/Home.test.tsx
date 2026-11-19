import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { HttpResponse, http } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '@/test/server'
import { config } from '@/lib/config'
import { LAST_FLAT_STORAGE_KEY } from '@/lib/last-flat'
import { Home } from './Home'

const flatsUrl = `${config.apiUrl}/api/v1/flats`

const flat = (id: string) => ({
  id,
  name: `Piso ${id}`,
  createdAt: '2026-11-19T20:00:00Z',
  role: 'Member',
})

function LocationProbe() {
  return <p data-testid="location">{useLocation().pathname}</p>
}

function renderHome() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/']}>
        <LocationProbe />
        <Routes>
          <Route index element={<Home />} />
          <Route path="/f/:flatId" element={<p>flat page</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('home redirect', () => {
  beforeEach(() => {
    window.localStorage.clear()
  })

  it('opens the last-used flat when the member still belongs to it', async () => {
    server.use(http.get(flatsUrl, () => HttpResponse.json([flat('f1'), flat('f2')])))
    window.localStorage.setItem(LAST_FLAT_STORAGE_KEY, 'f2')
    renderHome()

    expect(await screen.findByText('flat page')).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/f/f2')
  })

  it('falls back to the first flat when the remembered flat is no longer visible', async () => {
    server.use(http.get(flatsUrl, () => HttpResponse.json([flat('f1'), flat('f2')])))
    window.localStorage.setItem(LAST_FLAT_STORAGE_KEY, 'gone')
    renderHome()

    await screen.findByText('flat page')
    expect(screen.getByTestId('location')).toHaveTextContent('/f/f1')
  })
})
