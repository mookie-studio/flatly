import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { AuthContext, type AuthState } from '@/auth/auth-context'
import { config } from '@/lib/config'
import { Login } from './Login'

function renderLogin(path: string, auth: AuthState) {
  render(
    <AuthContext.Provider value={auth}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/rooms/:id" element={<h1>Room</h1>} />
          <Route path="/" element={<h1>Home</h1>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('Login', () => {
  it('offers Google and Microsoft sign-in that return to the requested page', () => {
    renderLogin('/login?redirect=%2Frooms%2F1%3Ftab%3Dweek', { status: 'anonymous' })

    const google = screen.getByRole('link', { name: 'Continue with Google' })
    const microsoft = screen.getByRole('link', { name: 'Continue with Microsoft' })
    expect(new URL(google.getAttribute('href')!).origin).toBe(new URL(config.apiUrl).origin)
    expect(new URL(google.getAttribute('href')!).pathname).toBe(
      '/api/v1/platform/auth/google/sign-in',
    )
    expect(new URL(google.getAttribute('href')!).searchParams.get('returnUrl')).toBe(
      '/rooms/1?tab=week',
    )
    expect(new URL(microsoft.getAttribute('href')!).pathname).toBe(
      '/api/v1/platform/auth/microsoft/sign-in',
    )
  })

  it('ignores a redirect to another site and returns to the home page instead', () => {
    renderLogin('/login?redirect=https%3A%2F%2Fevil.example', { status: 'anonymous' })

    const google = screen.getByRole('link', { name: 'Continue with Google' })
    expect(new URL(google.getAttribute('href')!).searchParams.get('returnUrl')).toBe('/')
  })

  it('sends a visitor who is already signed in straight to their destination', () => {
    renderLogin('/login?redirect=%2Frooms%2F7', { status: 'authenticated', userId: 'u1' })

    expect(screen.getByRole('heading', { name: 'Room' })).toBeInTheDocument()
  })
})
