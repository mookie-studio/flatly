import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, unwrap } from './client'
import type { components } from './schema'

export type Flat = components['schemas']['FlatResponse']

export const flatsKey = ['flats'] as const

/** The flats the signed-in member belongs to, oldest first. */
export const useMyFlats = () =>
  useQuery({
    queryKey: flatsKey,
    queryFn: () => unwrap(api.GET('/api/v1/flats')),
  })

/**
 * Creates a flat, and the signed-in member becomes its admin. The flat list is refetched before
 * the mutation settles, so the screen that offered the choice leaves once the flat exists.
 */
export const useCreateFlat = () => {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (name: string) => unwrap(api.POST('/api/v1/flats', { body: { name } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: flatsKey }),
  })
}

export type FlatMember = components['schemas']['FlatMemberResponse']

export const flatKey = (flatId: string) => ['flats', flatId] as const
export const flatMembersKey = (flatId: string) => ['flats', flatId, 'members'] as const

/** One flat the signed-in member belongs to. Flats the member does not belong to answer 404. */
export const useFlat = (flatId: string) =>
  useQuery({
    queryKey: flatKey(flatId),
    queryFn: () => unwrap(api.GET('/api/v1/flats/{flatId}', { params: { path: { flatId } } })),
  })

/** The active members of a flat, admins first. */
export const useFlatMembers = (flatId: string) =>
  useQuery({
    queryKey: flatMembersKey(flatId),
    queryFn: () =>
      unwrap(api.GET('/api/v1/flats/{flatId}/members', { params: { path: { flatId } } })),
  })
