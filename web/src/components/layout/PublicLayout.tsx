import { Outlet } from 'react-router'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { ThemeToggle } from '@/theme/ThemeToggle'
import { BrandLink } from './BrandLink'

/** Shell for pages that do not need a session, such as login. */
export function PublicLayout() {
  return (
    <div className="flex min-h-dvh flex-col">
      <header className="border-b border-line bg-surface-raised">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 px-4 py-3 md:px-8">
          <BrandLink />
          <div className="flex flex-wrap items-center gap-3">
            <LanguageSwitcher />
            <ThemeToggle />
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6 md:px-8">
        <Outlet />
      </main>
    </div>
  )
}
