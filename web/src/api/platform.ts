import { useQuery } from '@tanstack/react-query'
import { api, unwrap } from './client'
import type { components } from './schema'

export type ClockSample = components['schemas']['ClockSampleResponse']

export const clockSampleKey = ['platform', 'clock-sample'] as const

/** Sample query over the generated client: the server's current time. */
export const useClockSample = () =>
  useQuery({
    queryKey: clockSampleKey,
    queryFn: () => unwrap(api.GET('/api/v1/platform/clock-sample')),
  })
