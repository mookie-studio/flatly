export interface NavItem {
  to: string
  /** Key in the `shell` namespace, so the label follows the chosen language. */
  labelKey: string
}

/** Primary destinations shown in the shell. Grows as modules land. */
export const primaryNav: NavItem[] = [
  { to: '/', labelKey: 'nav.home' },
  { to: '/profile', labelKey: 'nav.profile' },
]

/** Home of one flat. The active flat travels in the URL, so every module lives under it. */
export const flatHomePath = (flatId: string) => `/f/${encodeURIComponent(flatId)}`

export interface FlatModuleLink {
  /** Key in the `home` namespace. */
  labelKey: string
  path: (flatId: string) => string
}

/**
 * Modules shown as shortcuts on a flat's home. A module is added here only once its screens
 * exist; modules not built yet stay out of the list instead of showing placeholders.
 */
export const flatModuleLinks: FlatModuleLink[] = []
