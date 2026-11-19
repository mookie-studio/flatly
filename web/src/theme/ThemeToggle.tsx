import { useTranslation } from 'react-i18next'
import { useTheme } from './ThemeProvider'
import { type ThemePreference } from './theme'

const options: ThemePreference[] = ['system', 'light', 'dark']

/** Segmented control for the theme choice. Each option is a toggle button, so screen readers announce the state. */
export function ThemeToggle() {
  const { t } = useTranslation('shell')
  const { preference, setPreference } = useTheme()

  return (
    <div
      role="group"
      aria-label={t('theme.label')}
      className="inline-flex rounded-lg border border-line bg-surface-raised p-0.5"
    >
      {options.map((value) => {
        const selected = value === preference
        return (
          <button
            key={value}
            type="button"
            aria-pressed={selected}
            onClick={() => setPreference(value)}
            className={`min-h-9 min-w-9 rounded-md px-3 text-sm font-medium transition-colors ${
              selected
                ? 'bg-accent text-accent-ink'
                : 'text-ink-muted hover:bg-accent-soft hover:text-ink'
            }`}
          >
            {t(`theme.${value}`)}
          </button>
        )
      })}
    </div>
  )
}
