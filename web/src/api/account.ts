import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { downloadJson } from '@/lib/download'
import { api, unwrap } from './client'
import { sessionKey } from '@/auth/use-session'
import type { components } from './schema'

export type Profile = components['schemas']['ProfileResponse']
export type ProfileUpdate = components['schemas']['UpdateProfileRequest']

export const profileKey = ['profile'] as const

/** The signed-in member's profile: name, avatar, language and time zone. */
export const useProfile = () =>
  useQuery({
    queryKey: profileKey,
    queryFn: () => unwrap(api.GET('/api/v1/platform/profile')),
  })

/** Replaces the profile. Every field is sent, because the API overwrites all of them. */
export const useUpdateProfile = () => {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (profile: ProfileUpdate) =>
      unwrap(api.PUT('/api/v1/platform/profile', { body: profile })),
    onSuccess: (saved) => queryClient.setQueryData(profileKey, saved),
  })
}

/** Downloads the member's data as a JSON file. */
export const useExportAccount = () =>
  useMutation({
    mutationFn: async () => {
      const data = await unwrap(api.GET('/api/v1/platform/account/export'))
      downloadJson('flatly-data.json', data)
    },
  })

/**
 * Deletes the account. The API needs the antiforgery token in a header, so it is fetched first.
 * On success the cached data of the member who left is dropped, as on sign-out.
 */
export const useDeleteAccount = () => {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async () => {
      const { requestToken } = await unwrap(api.GET('/api/v1/platform/auth/antiforgery-token'))
      await unwrap(
        api.DELETE('/api/v1/platform/account', {
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
