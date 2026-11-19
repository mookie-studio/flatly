import { useTranslation } from 'react-i18next'
import { NavLink, Outlet } from 'react-router'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { ThemeToggle } from '@/theme/ThemeToggle'
import { BrandLink } from './BrandLink'
import { primaryNav } from './navigation'

/**
 * Shell for signed-in pages. On phones the navigation sits in a bottom bar within thumb reach;
 * from the medium breakpoint up it moves to the header.
 */
export function AppLayout() {
  const { t } = useTranslation('shell')

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-10 border-b border-line bg-surface-raised/95 backdrop-blur">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 px-4 py-3 md:px-8">
          <BrandLink />
          <nav aria-label={t('nav.primary')} className="hidden md:block">
            <ul className="flex items-center gap-1">
              {primaryNav.map((item) => (
                <li key={item.to}>
                  <NavLink
                    to={item.to}
                    end
                    className={({ isActive }) =>
                      `inline-flex min-h-9 items-center rounded-md px-3 text-sm font-medium transition-colors ${
                        isActive ? 'bg-accent-soft text-ink' : 'text-ink-muted hover:text-ink'
                      }`
                    }
                  >
                    {t(item.labelKey)}
                  </NavLink>
                </li>
              ))}
            </ul>
          </nav>
          <div className="flex flex-wrap items-center gap-3">
            <LanguageSwitcher />
            <ThemeToggle />
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6 pb-24 md:px-8 md:pb-10">
        <Outlet />
      </main>

      <nav
        aria-label={t('nav.primary')}
        className="fixed inset-x-0 bottom-0 z-10 border-t border-line bg-surface-raised md:hidden"
      >
        <ul className="mx-auto flex max-w-md">
          {primaryNav.map((item) => (
            <li key={item.to} className="flex-1">
              <NavLink
                to={item.to}
                end
                className={({ isActive }) =>
                  `flex min-h-14 items-center justify-center text-sm font-medium ${
                    isActive ? 'text-accent' : 'text-ink-muted'
                  }`
                }
              >
                {t(item.labelKey)}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </div>
  )
}
