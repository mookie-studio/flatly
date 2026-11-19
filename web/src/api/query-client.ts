import { QueryClient } from '@tanstack/react-query'
import { isApiError } from './errors'

const MAX_RETRIES = 2

/** Client errors are final, 401 included (the session handler sends the user to login). Network and 5xx errors retry. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (isApiError(error) && error.status >= 400 && error.status < 500) return false
  return failureCount < MAX_RETRIES
}

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: shouldRetry, staleTime: 30_000 },
      mutations: { retry: false },
    },
  })
}
