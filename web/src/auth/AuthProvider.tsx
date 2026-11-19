import type { ReactNode } from 'react'
import { AuthContext } from './auth-context'
import { useSession } from './use-session'

/** Provides the session state to the router and every page. Must sit inside the query client provider. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const state = useSession()
  return <AuthContext.Provider value={state}>{children}</AuthContext.Provider>
}
