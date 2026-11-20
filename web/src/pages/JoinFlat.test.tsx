import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { HttpResponse, http } from 'msw'
import { server } from '@/test/server'
import { config } from '@/lib/config'
import { JoinFlat } from './JoinFlat'

const joinUrl = `${config.apiUrl}/api/v1/flats/join`

function CurrentPath() {
  return <p data-testid="path">{useLocation().pathname}</p>
}

function renderJoin(path: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/join" element={<JoinFlat />} />
          <Route path="/f/:flatId" element={<CurrentPath />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('join a flat', () => {
  it('joins from an invite link on arrival and lands on the flat', async () => {
    let body: unknown
    server.use(
      http.post(joinUrl, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({
          id: 'f1',
          name: 'Calle Mayor 12',
          createdAt: '2026-11-19T20:00:00Z',
          role: 'Member',
        })
      }),
    )
    renderJoin('/join?code=abcd2345')

    expect(await screen.findByTestId('path')).toHaveTextContent('/f/f1')
    expect(body).toEqual({ code: 'ABCD2345' })
  })

  it('explains an unknown code and keeps the code in the field for another try', async () => {
    server.use(
      http.post(joinUrl, () =>
        HttpResponse.json({ title: 'Invite code not found.' }, { status: 404 }),
      ),
    )
    renderJoin('/join?code=ABCD2345')

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('We could not find a flat with this invite code.')
    expect(alert).toHaveTextContent('Check the code for typos')
    expect(screen.getByLabelText('Invite code')).toHaveValue('ABCD2345')
    expect(screen.getByRole('button', { name: 'Join flat' })).toBeEnabled()
  })

  it('explains an expired code with a hint to ask an admin for a new one', async () => {
    server.use(http.post(joinUrl, () => HttpResponse.json({ title: 'Expired.' }, { status: 410 })))
    renderJoin('/join?code=ABCD2345')

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('This invite code has expired.')
    expect(alert).toHaveTextContent('Ask an admin of the flat for a new invite code.')
  })

  it('asks for a short wait when attempts are throttled', async () => {
    server.use(http.post(joinUrl, () => HttpResponse.json({}, { status: 429 })))
    renderJoin('/join?code=ABCD2345')

    expect(await screen.findByRole('alert')).toHaveTextContent('Too many attempts in a short time.')
  })

  it('joins a code typed into the field, trimmed and in capitals', async () => {
    let body: unknown
    server.use(
      http.post(joinUrl, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({
          id: 'f2',
          name: 'Piso',
          createdAt: '2026-11-19T20:00:00Z',
          role: 'Member',
        })
      }),
    )
    renderJoin('/join')

    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Invite code'), {
      target: { value: '  abcd2345 ' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Join flat' }))

    expect(await screen.findByTestId('path')).toHaveTextContent('/f/f2')
    expect(body).toEqual({ code: 'ABCD2345' })
  })

  it('keeps the form open with a generic message when the API cannot be reached', async () => {
    server.use(http.post(joinUrl, () => HttpResponse.error()))
    renderJoin('/join')

    fireEvent.change(screen.getByLabelText('Invite code'), { target: { value: 'ABCD2345' } })
    fireEvent.click(screen.getByRole('button', { name: 'Join flat' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('We could not join the flat.')
    await waitFor(() => expect(screen.getByRole('button', { name: 'Join flat' })).toBeEnabled())
  })
})
