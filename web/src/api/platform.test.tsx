import { QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { HttpResponse, http } from 'msw'
import type { ReactNode } from 'react'
import { server } from '@/test/server'
import { createApiClient } from './client'
import { createQueryClient } from './query-client'
import { setSessionExpiredHandler } from './session'
import { useClockSample } from './platform'
import { config } from '@/lib/config'

const wrapper = ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={createQueryClient()}>{children}</QueryClientProvider>
)

const clockUrl = `${config.apiUrl}/api/v1/platform/clock-sample`

describe('useClockSample', () => {
  it('returns the time from the generated client', async () => {
    server.use(http.get(clockUrl, () => HttpResponse.json({ now: '2026-11-19T20:00:00Z' })))

    const { result } = renderHook(() => useClockSample(), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual({ now: '2026-11-19T20:00:00Z' })
  })

  it('does not retry a 401 and reports the session as expired', async () => {
    const expired = vi.fn()
    setSessionExpiredHandler(expired)
    let calls = 0
    server.use(
      http.get(clockUrl, () => {
        calls += 1
        return HttpResponse.json({ status: 401 }, { status: 401 })
      }),
    )

    const { result } = renderHook(() => useClockSample(), { wrapper })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(calls).toBe(1)
    expect(expired).toHaveBeenCalledTimes(1)
    setSessionExpiredHandler(() => {})
  })

  it('sends the session cookie with every request', async () => {
    server.use(http.get(clockUrl, () => HttpResponse.json({ now: '2026-11-19T20:00:00Z' })))
    const fetchSpy = vi.spyOn(globalThis, 'fetch')

    await createApiClient().GET('/api/v1/platform/clock-sample')

    const sent = fetchSpy.mock.calls[0]?.[0] as Request
    expect(sent.credentials).toBe('include')
    fetchSpy.mockRestore()
  })
})
