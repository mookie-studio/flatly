const DEFAULT_PATH = '/'

// Accepts only same-origin paths such as "/rooms/1?tab=week#top".
// Rejects absolute URLs, protocol-relative "//host" and "/\host" forms, and control characters.
export function safeRedirectPath(value: string | null | undefined): string {
  if (!value || !value.startsWith('/')) return DEFAULT_PATH
  if (value.startsWith('//') || value.startsWith('/\\')) return DEFAULT_PATH
  if ([...value].some((char) => char.charCodeAt(0) < 0x20 || char.charCodeAt(0) === 0x7f)) {
    return DEFAULT_PATH
  }
  return value
}
