import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { HttpResponse, http } from 'msw'
import { i18n } from '@/i18n'
import { config } from '@/lib/config'
import { server } from '@/test/server'
import { isSecureUrl, Profile, timeZoneOptions } from './Profile'

const profileUrl = `${config.apiUrl}/api/v1/platform/profile`
const exportUrl = `${config.apiUrl}/api/v1/platform/account/export`
const accountUrl = `${config.apiUrl}/api/v1/platform/account`
const antiforgeryUrl = `${config.apiUrl}/api/v1/platform/auth/antiforgery-token`

const savedProfile = {
  displayName: 'Mookie',
  avatarUrl: null,
  preferredLanguage: 'en',
  timeZoneId: 'Europe/Madrid',
}

function CurrentPath() {
  return <p data-testid="path">{useLocation().pathname}</p>
}

function renderProfile() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/profile']}>
        <Routes>
          <Route path="/profile" element={<Profile />} />
          <Route path="/login" element={<CurrentPath />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('profile settings', () => {
  it('shows the saved profile and saves the edits with a trimmed name', async () => {
    let body: unknown
    server.use(
      http.get(profileUrl, () => HttpResponse.json(savedProfile)),
      http.put(profileUrl, async ({ request }) => {
        const sent = await request.json()
        body = sent
        return HttpResponse.json(savedProfile)
      }),
    )
    renderProfile()

    const name = await screen.findByLabelText('Display name')
    expect(name).toHaveValue('Mookie')
    expect(screen.getByLabelText('Time zone')).toHaveValue('Europe/Madrid')

    fireEvent.change(name, { target: { value: '  Ana  ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Your changes are saved.')
    expect(body).toEqual({
      displayName: 'Ana',
      avatarUrl: null,
      preferredLanguage: 'en',
      timeZoneId: 'Europe/Madrid',
    })
  })

  it('switches the interface language as soon as the new language is saved', async () => {
    server.use(
      http.get(profileUrl, () => HttpResponse.json(savedProfile)),
      http.put(profileUrl, async ({ request }) => HttpResponse.json(await request.json())),
    )
    renderProfile()

    fireEvent.change(await screen.findByLabelText('Interface language'), {
      target: { value: 'es' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByRole('heading', { name: 'Perfil y ajustes' })).toBeInTheDocument()
    expect(i18n.language).toBe('es')
  })

  it('does not save an avatar that is not a secure link', async () => {
    server.use(http.get(profileUrl, () => HttpResponse.json(savedProfile)))
    renderProfile()

    fireEvent.change(await screen.findByLabelText('Avatar URL'), {
      target: { value: 'http://example.com/me.png' },
    })

    expect(screen.getByRole('alert')).toHaveTextContent('The avatar must be a secure (https) link.')
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeDisabled()
  })

  it('downloads the data export as a JSON file', async () => {
    const createObjectURL = vi.fn<(blob: Blob) => string>(() => 'blob:export')
    const revokeObjectURL = vi.fn()
    Object.assign(URL, { createObjectURL, revokeObjectURL })
    server.use(
      http.get(profileUrl, () => HttpResponse.json(savedProfile)),
      http.get(exportUrl, () =>
        HttpResponse.json({ exportedAt: '2026-11-19T20:00:00Z', profile: {}, memberships: [] }),
      ),
    )
    renderProfile()

    fireEvent.click(await screen.findByRole('button', { name: 'Download my data' }))

    await waitFor(() => expect(createObjectURL).toHaveBeenCalledTimes(1))
    const blob = createObjectURL.mock.calls[0]![0] as Blob
    expect(blob.type).toBe('application/json')
    expect(JSON.parse(await blob.text())).toMatchObject({ memberships: [] })
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:export')
  })

  it('asks for confirmation before deleting, then signs the member out and leaves', async () => {
    let deleteCalls = 0
    let deleteHeader: string | null = null
    server.use(
      http.get(profileUrl, () => HttpResponse.json(savedProfile)),
      http.get(antiforgeryUrl, () => HttpResponse.json({ requestToken: 'token-1' })),
      http.delete(accountUrl, ({ request }) => {
        deleteCalls += 1
        deleteHeader = request.headers.get('X-XSRF-TOKEN')
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderProfile()

    fireEvent.click(await screen.findByRole('button', { name: 'Delete my account' }))
    expect(deleteCalls).toBe(0)
    fireEvent.click(screen.getByRole('button', { name: 'Yes, delete my account' }))

    expect(await screen.findByTestId('path')).toHaveTextContent('/login')
    expect(deleteCalls).toBe(1)
    expect(deleteHeader).toBe('token-1')
  })

  it('explains the last-admin rule and keeps the account when the member is the last admin', async () => {
    server.use(
      http.get(profileUrl, () => HttpResponse.json(savedProfile)),
      http.get(antiforgeryUrl, () => HttpResponse.json({ requestToken: 'token-1' })),
      http.delete(accountUrl, () =>
        HttpResponse.json(
          {
            title: 'Last admin of a flat',
            status: 409,
            detail:
              'Make another member an admin of these flats before deleting your account: Calle Mayor 12.',
          },
          { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    )
    renderProfile()

    fireEvent.click(await screen.findByRole('button', { name: 'Delete my account' }))
    fireEvent.click(screen.getByRole('button', { name: 'Yes, delete my account' }))

    expect(await screen.findByText(/You are the last admin of a flat/)).toBeInTheDocument()
    expect(screen.getByText(/Calle Mayor 12/)).toBeInTheDocument()
    expect(screen.queryByTestId('path')).not.toBeInTheDocument()
  })
})

describe('profile helpers', () => {
  it('accepts only secure absolute links', () => {
    expect(isSecureUrl('https://cdn.example.com/a.png')).toBe(true)
    expect(isSecureUrl('http://cdn.example.com/a.png')).toBe(false)
    expect(isSecureUrl('not a url')).toBe(false)
  })

  it('keeps the current time zone in the choices even when the browser does not list it', () => {
    expect(timeZoneOptions('Mars/Olympus')[0]).toBe('Mars/Olympus')
    expect(timeZoneOptions('Europe/Madrid')).toContain('Europe/Madrid')
  })
})
