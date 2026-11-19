import { createContext, useContext } from 'react'

export type AuthState =
  { status: 'loading' } | { status: 'anonymous' } | { status: 'authenticated'; userId: string }

// Placeholder session until the real provider lands with sign-in (#11).
// Treated as anonymous, so protected routes send visitors to login.
export const AuthContext = createContext<AuthState>({ status: 'anonymous' })

export function useAuth(): AuthState {
  return useContext(AuthContext)
}
