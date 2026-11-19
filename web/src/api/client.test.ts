import { HttpResponse, http } from 'msw'
import { server } from '@/test/server'
import { config } from '@/lib/config'
import { ApiError, isApiError } from './errors'
import { unwrap, api } from './client'
import { setSessionExpiredHandler } from './session'

describe('unwrap', () => {
  it('turns a problem response into an ApiError with its status', async () => {
    server.use(
      http.get(`${config.apiUrl}/api/v1/platform/clock-sample`, () =>
        HttpResponse.json({ title: 'Forbidden' }, { status: 403 }),
      ),
    )

    const error = await unwrap(api.GET('/api/v1/platform/clock-sample')).catch((e) => e)

    expect(isApiError(error)).toBe(true)
    expect((error as ApiError).status).toBe(403)
  })

  it('reports an unreachable API with status 0', async () => {
    server.use(
      http.get(`${config.apiUrl}/api/v1/platform/clock-sample`, () => HttpResponse.error()),
    )

    const error = await unwrap(api.GET('/api/v1/platform/clock-sample')).catch((e) => e)

    expect((error as ApiError).status).toBe(0)
  })

  it('notifies the session handler on 401 and not on other errors', async () => {
    const expired = vi.fn()
    setSessionExpiredHandler(expired)
    server.use(
      http.get(`${config.apiUrl}/api/v1/platform/clock-sample`, () =>
        HttpResponse.json({}, { status: 500 }),
      ),
    )

    await unwrap(api.GET('/api/v1/platform/clock-sample')).catch(() => undefined)
    expect(expired).not.toHaveBeenCalled()

    server.use(
      http.get(`${config.apiUrl}/api/v1/platform/clock-sample`, () =>
        HttpResponse.json({}, { status: 401 }),
      ),
    )
    await unwrap(api.GET('/api/v1/platform/clock-sample')).catch(() => undefined)
    expect(expired).toHaveBeenCalledTimes(1)
    setSessionExpiredHandler(() => {})
  })
})
