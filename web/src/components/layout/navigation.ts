export interface NavItem {
  to: string
  /** Key in the `shell` namespace, so the label follows the chosen language. */
  labelKey: string
}

/** Primary destinations shown in the shell. Grows as modules land. */
export const primaryNav: NavItem[] = [{ to: '/', labelKey: 'nav.home' }]
