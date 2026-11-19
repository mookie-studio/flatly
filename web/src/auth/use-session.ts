import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, unwrap } from '@/api/client'
import { isApiError } from '@/api/errors'
import { shouldRetry } from '@/api/query-client'
import type { AuthState } from './auth-context'

export const sessionKey = ['auth', 'session'] as const

/** The signed-in member, or null when the API answers 401 (no session). */
async function fetchSession() {
  try {
    return await unwrap(api.GET('/api/v1/platform/auth/session'))
  } catch (error) {
    if (isApiError(error) && error.status === 401) return null
    throw error
  }
}

/**
 * Session restore: asks the API who is signed in, so a reload keeps the member signed in.
 * While the answer is pending the state is `loading`; a failed check counts as signed out.
 */
export function useSession(): AuthState {
  const query = useQuery({ queryKey: sessionKey, queryFn: fetchSession, retry: shouldRetry })

  if (query.isPending) return { status: 'loading' }
  if (query.isError || query.data == null) return { status: 'anonymous' }
  return { status: 'authenticated', userId: query.data.userId }
}

/**
 * Ends the session. The API needs the antiforgery token in a header, so it is fetched first.
 * On success every cached query is dropped, because it belonged to the member who just left.
 */
export function useSignOut() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async () => {
      const { requestToken } = await unwrap(api.GET('/api/v1/platform/auth/antiforgery-token'))
      await unwrap(
        api.POST('/api/v1/platform/auth/sign-out', {
          params: { header: { 'X-XSRF-TOKEN': requestToken } },
        }),
      )
    },
    onSuccess: () => {
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== 'auth' })
      queryClient.setQueryData(sessionKey, null)
    },
  })
}
