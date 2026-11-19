import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { describe, expect, it } from 'vitest'
import { ThemeProvider } from '@/theme/ThemeProvider'
import { AppLayout } from './AppLayout'

function renderShell() {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <ThemeProvider>
        <MemoryRouter initialEntries={['/']}>
          <Routes>
            <Route element={<AppLayout />}>
              <Route index element={<h1>Home</h1>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </ThemeProvider>
    </QueryClientProvider>,
  )
}

describe('AppLayout', () => {
  it('shows the brand, the page content and the theme choice', () => {
    renderShell()

    expect(screen.getByRole('link', { name: 'Flatly' })).toHaveAttribute('href', '/')
    expect(screen.getByRole('heading', { name: 'Home' })).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'Theme' })).toBeInTheDocument()
  })

  it('offers the primary navigation on phones and on desktops', () => {
    renderShell()

    const navs = screen.getAllByRole('navigation', { name: 'Primary' })
    expect(navs).toHaveLength(2)
    navs.forEach((nav) => {
      expect(within(nav).getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/')
    })
  })
})
