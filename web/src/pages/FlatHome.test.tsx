import { beforeEach, describe, expect, it, vi } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { HttpResponse, http } from 'msw'
import { server } from '@/test/server'
import { config } from '@/lib/config'
import { LAST_FLAT_STORAGE_KEY } from '@/lib/last-flat'
import { FlatHome } from './FlatHome'

const flatUrl = `${config.apiUrl}/api/v1/flats/:flatId`
const membersUrl = `${config.apiUrl}/api/v1/flats/:flatId/members`
const sessionUrl = `${config.apiUrl}/api/v1/platform/auth/session`
const memberUrl = `${config.apiUrl}/api/v1/flats/:flatId/members/:userId`
const leaveUrl = `${config.apiUrl}/api/v1/flats/:flatId/leave`

const callerId = '00000000-0000-0000-0000-00000000000a'
const otherId = '00000000-0000-0000-0000-00000000000b'

const sessionHandler = () =>
  http.get(sessionUrl, () =>
    HttpResponse.json({
      userId: callerId,
      displayName: 'Ana',
      avatarUrl: null,
      preferredLanguage: 'en',
      timeZoneId: 'UTC',
    }),
  )

const flatAs = (role: 'Admin' | 'Member') =>
  http.get(flatUrl, () =>
    HttpResponse.json({
      id: 'f1',
      name: 'Calle Mayor 12',
      createdAt: '2026-11-19T20:00:00Z',
      role,
    }),
  )

const members = [
  {
    userId: callerId,
    displayName: 'Ana',
    avatarUrl: null,
    role: 'Admin',
    joinedAt: '2026-11-19T20:00:00Z',
  },
  {
    userId: otherId,
    displayName: 'Luis',
    avatarUrl: null,
    role: 'Member',
    joinedAt: '2026-11-19T20:05:00Z',
  },
]

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

const inviteUrl = `${config.apiUrl}/api/v1/flats/:flatId/invite-code`

describe('flat home', () => {
  beforeEach(() => {
    window.localStorage.clear()
    server.use(
      http.get(inviteUrl, () =>
        HttpResponse.json({ code: 'ABCD2345', expiresAt: '2026-11-27T00:00:00Z' }),
      ),
    )
  })

  it('offers invite sharing to admins only', async () => {
    server.use(
      sessionHandler(),
      flatAs('Admin'),
      http.get(membersUrl, () => HttpResponse.json(members)),
    )
    renderFlat('/f/f1')
    expect(await screen.findByRole('heading', { name: 'Invite members' })).toBeInTheDocument()
    expect(await screen.findByText('ABCD2345')).toBeInTheDocument()
  })

  it('keeps the invite code from members who are not admins', async () => {
    let requested = false
    server.use(
      sessionHandler(),
      flatAs('Member'),
      http.get(membersUrl, () => HttpResponse.json(members)),
      http.get(inviteUrl, () => {
        requested = true
        return HttpResponse.json({ code: 'ABCD2345', expiresAt: '2026-11-27T00:00:00Z' })
      }),
    )
    renderFlat('/f/f1')
    expect(await screen.findByRole('heading', { name: 'Calle Mayor 12' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Invite members' })).not.toBeInTheDocument()
    expect(requested).toBe(false)
  })

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

  it('asks for confirmation before removing a member, then calls the API', async () => {
    const removed = vi.fn()
    server.use(
      sessionHandler(),
      flatAs('Admin'),
      http.get(membersUrl, () => HttpResponse.json(members)),
      http.delete(memberUrl, ({ params }) => {
        removed(params.userId)
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderFlat('/f/f1')

    const row = (await screen.findByText('Luis')).closest('li') as HTMLElement
    fireEvent.click(within(row).getByRole('button', { name: 'Remove from flat' }))
    expect(removed).not.toHaveBeenCalled()
    expect(screen.getByRole('alertdialog')).toHaveTextContent(
      'Luis will be removed from this flat.',
    )

    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))
    await vi.waitFor(() => expect(removed).toHaveBeenCalledWith(otherId))
  })

  it('shows the last-admin message when leaving would leave the flat without an admin', async () => {
    server.use(
      sessionHandler(),
      flatAs('Admin'),
      http.get(membersUrl, () => HttpResponse.json(members)),
      http.post(leaveUrl, () => new HttpResponse(null, { status: 409 })),
    )
    renderFlat('/f/f1')

    fireEvent.click(await screen.findByRole('button', { name: 'Leave flat' }))
    fireEvent.click(screen.getByRole('button', { name: 'Confirm' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'A flat needs at least one admin. Make another member an admin first.',
    )
  })

  it('hides management actions from members who are not admins', async () => {
    server.use(
      sessionHandler(),
      flatAs('Member'),
      http.get(membersUrl, () => HttpResponse.json(members)),
    )
    renderFlat('/f/f1')

    expect(await screen.findByRole('button', { name: 'Leave flat' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Remove from flat' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Make admin' })).not.toBeInTheDocument()
  })

  it('remembers the flat once the member can see it', async () => {
    server.use(
      http.get(flatUrl, () =>
        HttpResponse.json({
          id: 'f1',
          name: 'Calle Mayor 12',
          createdAt: '2026-11-19T20:00:00Z',
          role: 'Member',
        }),
      ),
      http.get(membersUrl, () => HttpResponse.json([])),
    )
    renderFlat('/f/f1')

    await screen.findByRole('heading', { name: 'Calle Mayor 12' })
    expect(window.localStorage.getItem(LAST_FLAT_STORAGE_KEY)).toBe('f1')
  })

  it('does not remember a flat the member cannot see', async () => {
    server.use(
      http.get(flatUrl, () => HttpResponse.json({ title: 'Not found' }, { status: 404 })),
      http.get(membersUrl, () => HttpResponse.json({ title: 'Not found' }, { status: 404 })),
    )
    renderFlat('/f/someone-elses')

    await screen.findByRole('alert')
    expect(window.localStorage.getItem(LAST_FLAT_STORAGE_KEY)).toBeNull()
  })
})
