import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from '@/auth/auth-context'

export function RequireAuth() {
  const auth = useAuth()
  const location = useLocation()

  if (auth.status === 'loading') {
    return <p role="status">Loading…</p>
  }

  if (auth.status === 'anonymous') {
    const destination = location.pathname + location.search + location.hash
    const params = new URLSearchParams({ redirect: destination })
    return <Navigate to={`/login?${params.toString()}`} replace />
  }

  return <Outlet />
}
