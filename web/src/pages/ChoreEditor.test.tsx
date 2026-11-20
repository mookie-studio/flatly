import { describe, expect, it, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { HttpResponse, http } from 'msw'
import { server } from '@/test/server'
import { config } from '@/lib/config'
import { ChoreEditor } from './ChoreEditor'

const base = config.apiUrl
const membersUrl = `${base}/api/v1/flats/:flatId/members`
const choresUrl = `${base}/api/v1/chores/:flatId`
const choreUrl = `${base}/api/v1/chores/:flatId/:choreId`

const anaId = '00000000-0000-0000-0000-00000000000a'
const luisId = '00000000-0000-0000-0000-00000000000b'
const choreId = '00000000-0000-0000-0000-0000000000c1'

const members = [
  {
    userId: anaId,
    displayName: 'Ana',
    avatarUrl: null,
    role: 'Admin',
    joinedAt: '2026-11-19T20:00:00Z',
  },
  {
    userId: luisId,
    displayName: 'Luis',
    avatarUrl: null,
    role: 'Member',
    joinedAt: '2026-11-19T20:05:00Z',
  },
]

const existingChore = {
  id: choreId,
  name: 'Bathroom',
  recurrenceUnit: 'Month',
  recurrenceEvery: 2,
  weight: 4,
  isActive: true,
  participantIds: [luisId],
  createdAt: '2026-11-19T20:00:00Z',
}

function renderEditor(path: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/f/:flatId" element={<p>flat home</p>} />
          <Route path="/f/:flatId/chores/new" element={<ChoreEditor />} />
          <Route path="/f/:flatId/chores/:choreId" element={<ChoreEditor />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('chore editor', () => {
  beforeEach(() => {
    server.use(http.get(membersUrl, () => HttpResponse.json(members)))
  })

  it('does not send anything while required fields are empty', async () => {
    const post = vi.fn(() => HttpResponse.json({}, { status: 201 }))
    server.use(http.post(choresUrl, post))
    renderEditor('/f/f1/chores/new')

    fireEvent.click(await screen.findByRole('button', { name: 'Create chore' }))
    fireEvent.change(screen.getByLabelText('Weight (effort, from 1 to 5)'), {
      target: { value: '9' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Create chore' }))

    expect(await screen.findByText('Enter a name for the chore.')).toBeInTheDocument()
    expect(screen.getByText('Choose a weight from 1 to 5.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })

  it('creates a chore with the chosen participants and returns to the flat', async () => {
    let body: unknown
    server.use(
      http.post(choresUrl, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(existingChore, { status: 201 })
      }),
    )
    renderEditor('/f/f1/chores/new')

    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: '  Bins  ' } })
    fireEvent.change(screen.getByLabelText('Every'), { target: { value: '2' } })
    fireEvent.change(screen.getByLabelText('Unit'), { target: { value: 'Month' } })
    fireEvent.change(screen.getByLabelText('Weight (effort, from 1 to 5)'), {
      target: { value: '3' },
    })
    fireEvent.click(screen.getByLabelText('Luis'))
    fireEvent.click(screen.getByRole('button', { name: 'Create chore' }))

    expect(await screen.findByText('flat home')).toBeInTheDocument()
    expect(body).toEqual({
      name: 'Bins',
      recurrenceUnit: 'Month',
      recurrenceEvery: 2,
      weight: 3,
      participantIds: [anaId],
    })
  })

  it('shows API field errors next to the field they belong to', async () => {
    server.use(
      http.post(choresUrl, () =>
        HttpResponse.json(
          {
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: {
              participantIds: ['Participants must be active members of the flat.'],
              weight: ['Weight must be 5 or less.'],
            },
          },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    )
    renderEditor('/f/f1/chores/new')

    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Bins' } })
    fireEvent.click(screen.getByRole('button', { name: 'Create chore' }))

    expect(
      await screen.findByText('You can only choose active members of the flat.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Weight (effort, from 1 to 5)')).toHaveAttribute(
      'aria-invalid',
      'true',
    )
  })

  it('edits a chore and deactivates it after confirmation', async () => {
    let patched: unknown
    server.use(
      http.get(choresUrl, () => HttpResponse.json([existingChore])),
      http.patch(choreUrl, async ({ request }) => {
        patched = await request.json()
        return HttpResponse.json({ ...existingChore, isActive: false })
      }),
    )
    renderEditor(`/f/f1/chores/${choreId}`)

    expect(await screen.findByRole('heading', { name: 'Edit chore' })).toBeInTheDocument()
    expect(screen.getByLabelText('Name')).toHaveValue('Bathroom')

    fireEvent.click(screen.getByRole('button', { name: 'Deactivate chore' }))
    const dialog = screen.getByRole('alertdialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Confirm' }))

    await waitFor(() => expect(patched).toEqual({ isActive: false }))
  })

  it('says so when the chore is not in the flat', async () => {
    server.use(http.get(choresUrl, () => HttpResponse.json([])))
    renderEditor(`/f/f1/chores/${choreId}`)

    expect(
      await screen.findByText('This chore does not exist or you are not a member of this flat.'),
    ).toBeInTheDocument()
  })
})
