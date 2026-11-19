import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from 'react-router/dom'
import './i18n'
import { setSessionExpiredHandler } from './api/session'
import { createQueryClient } from './api/query-client'
import { AuthProvider } from './auth/AuthProvider'
import { sessionKey } from './auth/use-session'
import { ThemeProvider } from './theme/ThemeProvider'
import { createRouter, loginHref } from './router'

const router = createRouter()
const queryClient = createQueryClient()

// An expired session (401 from the API) sends the visitor to login, keeping where they were.
setSessionExpiredHandler(() => {
  // Forget the cached session first, so a signed-out visitor is not sent back into the app.
  queryClient.setQueryData(sessionKey, null)
  const { location } = router.state
  if (location.pathname === '/login') return
  void router.navigate(loginHref(location), { replace: true })
})

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </ThemeProvider>
    </QueryClientProvider>
  )
}
