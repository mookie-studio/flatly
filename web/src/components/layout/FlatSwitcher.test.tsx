import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { HttpResponse, http } from 'msw'
import { server } from '@/test/server'
import { config } from '@/lib/config'
import { FlatSwitcher } from './FlatSwitcher'

const flatsUrl = `${config.apiUrl}/api/v1/flats`

const flat = (id: string, name: string) => ({
  id,
  name,
  createdAt: '2026-11-19T20:00:00Z',
  role: 'Member',
})

function LocationProbe() {
  const location = useLocation()
  return <p data-testid="location">{location.pathname}</p>
}

function renderAt(path: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <FlatSwitcher />
        <LocationProbe />
        <Routes>
          <Route path="/f/:flatId/*" element={<p>flat page</p>} />
          <Route path="*" element={<p>other page</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('flat switcher', () => {
  it('is hidden for a member with a single flat', async () => {
    server.use(http.get(flatsUrl, () => HttpResponse.json([flat('f1', 'Calle Mayor 12')])))
    renderAt('/f/f1')

    await screen.findByTestId('location')
    await waitFor(() => expect(screen.queryByRole('combobox')).not.toBeInTheDocument())
  })

  it('lists every flat and moves to the chosen one', async () => {
    server.use(
      http.get(flatsUrl, () =>
        HttpResponse.json([flat('f1', 'Calle Mayor 12'), flat('f2', 'Calle Sol 3')]),
      ),
    )
    renderAt('/f/f1')

    const select = await screen.findByRole('combobox', { name: 'Switch flat' })
    expect(select).toHaveValue('f1')
    expect(screen.getByRole('option', { name: 'Calle Sol 3' })).toBeInTheDocument()

    fireEvent.change(select, { target: { value: 'f2' } })

    expect(await screen.findByTestId('location')).toHaveTextContent('/f/f2')
    expect(screen.getByRole('combobox', { name: 'Switch flat' })).toHaveValue('f2')
  })

  it('offers a placeholder when the current page is not a flat', async () => {
    server.use(
      http.get(flatsUrl, () =>
        HttpResponse.json([flat('f1', 'Calle Mayor 12'), flat('f2', 'Calle Sol 3')]),
      ),
    )
    renderAt('/somewhere-else')

    const select = await screen.findByRole('combobox', { name: 'Switch flat' })
    expect(select).toHaveValue('')
    expect(screen.getByRole('option', { name: 'Choose a flat' })).toBeDisabled()
  })
})
