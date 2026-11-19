import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { RouterProvider } from 'react-router/dom'
import { HttpResponse, http } from 'msw'
import { server } from './test/server'
import { config } from './lib/config'
import { AuthContext, type AuthState } from './auth/auth-context'
import { createTestRouter } from './router'
import { ThemeProvider } from './theme/ThemeProvider'

function renderAt(path: string, auth: AuthState) {
  const router = createTestRouter(path)
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ThemeProvider>
        <AuthContext.Provider value={auth}>
          <RouterProvider router={router} />
        </AuthContext.Provider>
      </ThemeProvider>
    </QueryClientProvider>,
  )
  return router
}

describe('routing', () => {
  it('redirects an anonymous visit to a protected route to login', async () => {
    const router = renderAt('/', { status: 'anonymous' })

    expect(await screen.findByRole('heading', { name: 'Log in' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(new URLSearchParams(router.state.location.search).get('redirect')).toBe('/')
  })

  it('keeps path, query and hash of the intended destination in the redirect', async () => {
    const router = renderAt('/rooms/42?tab=week#today', { status: 'anonymous' })

    await screen.findByRole('heading', { name: 'Log in' })
    expect(new URLSearchParams(router.state.location.search).get('redirect')).toBe(
      '/rooms/42?tab=week#today',
    )
  })

  it('renders protected routes for an authenticated visitor', async () => {
    server.use(
      http.get(`${config.apiUrl}/api/v1/flats`, () =>
        HttpResponse.json([
          { id: 'f1', name: 'Calle Mayor 12', createdAt: '2026-11-19T20:00:00Z', role: 'Admin' },
        ]),
      ),
    )
    server.use(
      http.get(`${config.apiUrl}/api/v1/flats/f1`, () =>
        HttpResponse.json({
          id: 'f1',
          name: 'Calle Mayor 12',
          createdAt: '2026-11-19T20:00:00Z',
          role: 'Admin',
        }),
      ),
      http.get(`${config.apiUrl}/api/v1/flats/f1/members`, () => HttpResponse.json([])),
    )
    const router = renderAt('/', { status: 'authenticated', userId: 'user-1' })

    expect(await screen.findByRole('heading', { name: 'Calle Mayor 12' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/f/f1')
  })

  it('renders the public login page without a session and without redirecting', async () => {
    const router = renderAt('/login', { status: 'anonymous' })

    expect(await screen.findByRole('heading', { name: 'Log in' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('renders the not-found page inside the app for unknown paths when authenticated', async () => {
    renderAt('/does-not-exist', { status: 'authenticated', userId: 'user-1' })

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument()
  })
})
