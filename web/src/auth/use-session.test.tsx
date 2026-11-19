import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import { HttpResponse, http } from 'msw'
import type { ReactNode } from 'react'
import { config } from '@/lib/config'
import { server } from '@/test/server'
import { sessionKey, useSession, useSignOut } from './use-session'

const base = `${config.apiUrl}/api/v1/platform/auth`
const member = {
  userId: '6f1d2c3e-0000-4000-8000-000000000001',
  displayName: 'Ana',
  avatarUrl: null,
  preferredLanguage: 'es',
  timeZoneId: 'Europe/Madrid',
}

function wrapperFor(client: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}

describe('useSession', () => {
  it('restores the signed-in member on reload', async () => {
    server.use(http.get(`${base}/session`, () => HttpResponse.json(member)))
    const client = new QueryClient()

    const { result } = renderHook(() => useSession(), { wrapper: wrapperFor(client) })

    expect(result.current).toEqual({ status: 'loading' })
    await waitFor(() =>
      expect(result.current).toEqual({ status: 'authenticated', userId: member.userId }),
    )
  })

  it('treats a 401 from the session check as signed out', async () => {
    server.use(http.get(`${base}/session`, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useSession(), { wrapper: wrapperFor(new QueryClient()) })

    await waitFor(() => expect(result.current).toEqual({ status: 'anonymous' }))
  })
})

describe('useSignOut', () => {
  it('sends the antiforgery token and clears the session', async () => {
    let sentHeader: string | null = null
    server.use(
      http.get(`${base}/antiforgery-token`, () => HttpResponse.json({ requestToken: 'token-123' })),
      http.post(`${base}/sign-out`, ({ request }) => {
        sentHeader = request.headers.get('X-XSRF-TOKEN')
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const client = new QueryClient()
    client.setQueryData(sessionKey, member)
    client.setQueryData(['platform', 'clock-sample'], { now: 'x' })

    const { result } = renderHook(() => useSignOut(), { wrapper: wrapperFor(client) })
    await act(async () => {
      await result.current.mutateAsync()
    })

    expect(sentHeader).toBe('token-123')
    expect(client.getQueryData(sessionKey)).toBeNull()
    expect(client.getQueryData(['platform', 'clock-sample'])).toBeUndefined()
  })

  it('keeps the session and reports the error when sign-out fails', async () => {
    server.use(
      http.get(`${base}/antiforgery-token`, () => HttpResponse.json({ requestToken: 't' })),
      http.post(`${base}/sign-out`, () => new HttpResponse(null, { status: 500 })),
    )
    const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
    client.setQueryData(sessionKey, member)

    const { result } = renderHook(() => useSignOut(), { wrapper: wrapperFor(client) })
    act(() => result.current.mutate())

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(client.getQueryData(sessionKey)).toEqual(member)
  })
})
