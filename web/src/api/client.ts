import createClient, { type Middleware } from 'openapi-fetch'
import { config } from '@/lib/config'
import { ApiError } from './errors'
import { notifySessionExpired } from './session'
import type { paths } from './schema'

// Sessions travel in a cookie, so every request is sent with credentials.
const sessionMiddleware: Middleware = {
  async onResponse({ response }) {
    if (response.status === 401) notifySessionExpired()
    return undefined
  },
}

export function createApiClient(baseUrl = config.apiUrl) {
  // Look up fetch on each call, so mocks installed after this module loads (tests) are used.
  const client = createClient<paths>({
    baseUrl,
    fetch: (request) => globalThis.fetch(new Request(request, { credentials: 'include' })),
  })
  client.use(sessionMiddleware)
  return client
}

export const api = createApiClient()

/**
 * Unwraps an openapi-fetch result: returns `data` or throws an ApiError.
 * Usage: `const sample = await unwrap(api.GET('/api/v1/platform/clock-sample'))`.
 */
export async function unwrap<T>(
  request: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  let result
  try {
    result = await request
  } catch {
    // Network failure or CORS: the API is unreachable.
    throw new ApiError(0)
  }
  if (result.error !== undefined || !result.response.ok) {
    throw new ApiError(result.response.status, result.error)
  }
  return result.data as T
}
