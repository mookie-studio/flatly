import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { HttpResponse, http } from 'msw'
import { server } from '@/test/server'
import { config } from '@/lib/config'
import { Home } from './Home'

const flatsUrl = `${config.apiUrl}/api/v1/flats`

function CurrentPath() {
  return <p data-testid="path">{useLocation().pathname}</p>
}

function renderHome() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/f/:flatId" element={<CurrentPath />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('onboarding', () => {
  it('offers creating a flat and marks joining as unavailable for a member with no flat', async () => {
    server.use(http.get(flatsUrl, () => HttpResponse.json([])))
    renderHome()

    expect(await screen.findByRole('heading', { name: 'Welcome to Flatly' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Create a flat' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Join a flat' })).toBeDisabled()
  })

  it('creates a flat with the trimmed name and then lands on its home', async () => {
    type FlatBody = { id: string; name: string; createdAt: string; role: string }
    let flats: FlatBody[] = []
    let body: unknown
    server.use(
      http.get(flatsUrl, () => HttpResponse.json(flats)),
      http.post(flatsUrl, async ({ request }) => {
        body = await request.json()
        const created: FlatBody = {
          id: 'f1',
          name: 'Calle Mayor 12',
          createdAt: '2026-11-19T20:00:00Z',
          role: 'Admin',
        }
        flats = [created]
        return HttpResponse.json(created, { status: 201 })
      }),
    )
    renderHome()

    fireEvent.click(await screen.findByRole('button', { name: 'Create a flat' }))
    fireEvent.change(screen.getByLabelText('Flat name'), {
      target: { value: '  Calle Mayor 12  ' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Create flat' }))

    expect(await screen.findByTestId('path')).toHaveTextContent('/f/f1')
    expect(body).toEqual({ name: 'Calle Mayor 12' })
  })

  it('keeps the form and explains the failure when the flat cannot be created', async () => {
    server.use(
      http.get(flatsUrl, () => HttpResponse.json([])),
      http.post(flatsUrl, () => HttpResponse.json({ title: 'Server error' }, { status: 500 })),
    )
    renderHome()

    fireEvent.click(await screen.findByRole('button', { name: 'Create a flat' }))
    fireEvent.change(screen.getByLabelText('Flat name'), { target: { value: 'Piso' } })
    fireEvent.click(screen.getByRole('button', { name: 'Create flat' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('We could not create your flat')
    expect(screen.getByLabelText('Flat name')).toHaveValue('Piso')
    await waitFor(() => expect(screen.getByRole('button', { name: 'Create flat' })).toBeEnabled())
  })
})
