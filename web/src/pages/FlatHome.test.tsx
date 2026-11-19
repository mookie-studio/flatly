import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { HttpResponse, http } from 'msw'
import { server } from '@/test/server'
import { config } from '@/lib/config'
import { FlatHome } from './FlatHome'

const flatUrl = `${config.apiUrl}/api/v1/flats/:flatId`
const membersUrl = `${config.apiUrl}/api/v1/flats/:flatId/members`

function renderFlat(path: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/f/:flatId" element={<FlatHome />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('flat home', () => {
  it('shows the flat name and its active members with their roles', async () => {
    server.use(
      http.get(flatUrl, () =>
        HttpResponse.json({
          id: 'f1',
          name: 'Calle Mayor 12',
          createdAt: '2026-11-19T20:00:00Z',
          role: 'Member',
        }),
      ),
      http.get(membersUrl, () =>
        HttpResponse.json([
          {
            userId: 'u1',
            displayName: 'Ana',
            avatarUrl: null,
            role: 'Admin',
            joinedAt: '2026-11-19T20:00:00Z',
          },
          {
            userId: 'u2',
            displayName: 'Luis',
            avatarUrl: null,
            role: 'Member',
            joinedAt: '2026-11-19T20:05:00Z',
          },
        ]),
      ),
    )
    renderFlat('/f/f1')

    expect(await screen.findByRole('heading', { name: 'Calle Mayor 12' })).toBeInTheDocument()
    const members = screen.getByRole('list', { name: 'Members' })
    expect(members).toHaveTextContent('Ana')
    expect(members).toHaveTextContent('Admin')
    expect(members).toHaveTextContent('Luis')
    expect(members).toHaveTextContent('Member')
  })

  it('explains that the flat is unavailable when the caller is not a member', async () => {
    server.use(
      http.get(flatUrl, () => HttpResponse.json({ title: 'Not found' }, { status: 404 })),
      http.get(membersUrl, () => HttpResponse.json({ title: 'Not found' }, { status: 404 })),
    )
    renderFlat('/f/someone-elses')

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'This flat does not exist or you are not a member of it.',
    )
    expect(screen.queryByRole('heading')).not.toBeInTheDocument()
  })

  it('shows a generic error when the flat cannot be loaded for another reason', async () => {
    server.use(
      http.get(flatUrl, () => HttpResponse.json({ title: 'Server error' }, { status: 500 })),
      http.get(membersUrl, () => HttpResponse.json([])),
    )
    renderFlat('/f/f1')

    expect(await screen.findByRole('alert')).toHaveTextContent('We could not load your flat')
  })
})
