import { Outlet } from 'react-router'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'

export function PublicLayout() {
  return (
    <main>
      <LanguageSwitcher />
      <Outlet />
    </main>
  )
}
