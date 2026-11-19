/** What the user chose. `system` follows the operating system setting. */
export type ThemePreference = 'system' | 'light' | 'dark'

/** What is actually applied to the page. */
export type ResolvedTheme = 'light' | 'dark'

export const THEME_STORAGE_KEY = 'flatly.theme'

export function isThemePreference(value: unknown): value is ThemePreference {
  return value === 'system' || value === 'light' || value === 'dark'
}

/** Reads the saved choice. Storage can be blocked or missing, so any failure means `system`. */
export function readStoredTheme(): ThemePreference {
  try {
    const value = window.localStorage.getItem(THEME_STORAGE_KEY)
    return isThemePreference(value) ? value : 'system'
  } catch {
    return 'system'
  }
}

/** Saves the choice. Failing to save only means the choice is not remembered. */
export function storeTheme(preference: ThemePreference): void {
  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, preference)
  } catch {
    // Ignored on purpose: the theme still applies for this visit.
  }
}

export function systemPrefersDark(): boolean {
  return typeof window.matchMedia === 'function'
    ? window.matchMedia('(prefers-color-scheme: dark)').matches
    : false
}

export function resolveTheme(preference: ThemePreference, prefersDark: boolean): ResolvedTheme {
  if (preference === 'system') return prefersDark ? 'dark' : 'light'
  return preference
}

/** Writes the resolved theme to <html data-theme>, which the CSS tokens read. */
export function applyTheme(theme: ResolvedTheme): void {
  document.documentElement.dataset.theme = theme
}
