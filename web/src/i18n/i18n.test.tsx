import { fireEvent, render, screen } from '@testing-library/react'
import { RouterProvider } from 'react-router/dom'
import { AuthContext } from '@/auth/auth-context'
import { i18n, LANGUAGE_STORAGE_KEY } from '@/i18n'
import { createTestRouter } from '@/router'

function renderLogin() {
  const router = createTestRouter('/login')
  render(
    <AuthContext.Provider value={{ status: 'anonymous' }}>
      <RouterProvider router={router} />
    </AuthContext.Provider>,
  )
}

describe('language switch', () => {
  it('changes visible copy when the user picks another language', async () => {
    renderLogin()
    expect(await screen.findByRole('heading', { name: 'Log in' })).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Language'), { target: { value: 'es' } })

    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(screen.getByLabelText('Idioma')).toHaveValue('es')
    expect(document.documentElement.lang).toBe('es')
  })

  it('remembers the chosen language for the next visit', async () => {
    renderLogin()
    fireEvent.change(await screen.findByLabelText('Language'), { target: { value: 'es' } })

    await screen.findByRole('heading', { name: 'Iniciar sesión' })
    expect(localStorage.getItem(LANGUAGE_STORAGE_KEY)).toBe('es')
  })

  it('falls back to Spanish for languages the app does not support', async () => {
    await i18n.changeLanguage('fr')
    expect(i18n.t('login.title', { ns: 'auth', lng: 'fr' })).toBe('Iniciar sesión')
  })
})
