import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, unwrap } from './client'
import type { components } from './schema'

export type Chore = components['schemas']['ChoreResponse']
export type ChoreInput = components['schemas']['CreateChoreRequest']
export type ChoreChanges = components['schemas']['UpdateChoreRequest']

export const choresKey = (flatId: string) => ['flats', flatId, 'chores'] as const

/**
 * The chores of a flat, by name. Deactivated chores are listed only when `includeInactive` is set.
 * Pass `enabled` to skip the request, for screens that do not show chores yet.
 */
export const useChores = (flatId: string, includeInactive = false, enabled = true) =>
  useQuery({
    queryKey: [...choresKey(flatId), { includeInactive }],
    queryFn: () =>
      unwrap(
        api.GET('/api/v1/chores/{flatId}', {
          params: { path: { flatId }, query: { includeInactive } },
        }),
      ),
    enabled,
  })

/** Creates a chore. Its participants must be active members of the flat, otherwise the API answers 400. */
export const useCreateChore = (flatId: string) => {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (body: ChoreInput) =>
      unwrap(api.POST('/api/v1/chores/{flatId}', { params: { path: { flatId } }, body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: choresKey(flatId) }),
  })
}

/** Changes a chore, or deactivates it with `isActive: false`. Only the fields sent change. */
export const useUpdateChore = (flatId: string) => {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ choreId, changes }: { choreId: string; changes: ChoreChanges }) =>
      unwrap(
        api.PATCH('/api/v1/chores/{flatId}/{choreId}', {
          params: { path: { flatId, choreId } },
          body: changes,
        }),
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: choresKey(flatId) }),
  })
}
