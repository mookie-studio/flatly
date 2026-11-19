import { useTranslation } from 'react-i18next'
import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from '@/auth/auth-context'

export function RequireAuth() {
  const auth = useAuth()
  const location = useLocation()
  const { t } = useTranslation('common')

  if (auth.status === 'loading') {
    return <p role="status">{t('status.loading')}</p>
  }

  if (auth.status === 'anonymous') {
    const destination = location.pathname + location.search + location.hash
    const params = new URLSearchParams({ redirect: destination })
    return <Navigate to={`/login?${params.toString()}`} replace />
  }

  return <Outlet />
}
