/**
 * A failed API call. `status` is 0 when the API could not be reached at all.
 * `problem` is the RFC 9457 problem details body when the API sent one.
 */
export class ApiError extends Error {
  readonly status: number
  readonly problem: unknown

  constructor(status: number, problem?: unknown) {
    super(`API request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}
