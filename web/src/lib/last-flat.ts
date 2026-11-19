/** Per-device memory of the flat a member last opened. Only a convenience, so storage failures are silent. */
export const LAST_FLAT_STORAGE_KEY = 'flatly.lastFlatId'

export function readLastFlatId(): string | null {
  try {
    return window.localStorage.getItem(LAST_FLAT_STORAGE_KEY)
  } catch {
    // Storage can be blocked (private windows, disabled site data); the app then simply forgets.
    return null
  }
}

export function rememberLastFlatId(flatId: string): void {
  try {
    window.localStorage.setItem(LAST_FLAT_STORAGE_KEY, flatId)
  } catch {
    // Nothing to do: without storage the member lands on their first flat.
  }
}
