import { render, screen, fireEvent } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ThemeProvider } from './ThemeProvider'
import { ThemeToggle } from './ThemeToggle'
import { THEME_STORAGE_KEY } from './theme'

function stubSystemDark(prefersDark: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn().mockImplementation((query: string) => ({
      matches: prefersDark && query === '(prefers-color-scheme: dark)',
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })),
  )
}

function renderToggle() {
  return render(
    <ThemeProvider>
      <ThemeToggle />
    </ThemeProvider>,
  )
}

describe('ThemeProvider', () => {
  beforeEach(() => {
    window.localStorage.clear()
    delete document.documentElement.dataset.theme
    stubSystemDark(false)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('follows the system theme by default', () => {
    stubSystemDark(true)
    renderToggle()

    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(screen.getByRole('button', { name: 'System' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('applies and remembers an explicit choice', () => {
    renderToggle()

    fireEvent.click(screen.getByRole('button', { name: 'Dark' }))

    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
    expect(screen.getByRole('button', { name: 'Dark' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('restores the saved choice on the next visit', () => {
    window.localStorage.setItem(THEME_STORAGE_KEY, 'light')
    stubSystemDark(true)
    renderToggle()

    expect(document.documentElement.dataset.theme).toBe('light')
  })

  it('ignores an unknown saved value', () => {
    window.localStorage.setItem(THEME_STORAGE_KEY, 'neon')
    renderToggle()

    expect(screen.getByRole('button', { name: 'System' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('still works when storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    renderToggle()

    fireEvent.click(screen.getByRole('button', { name: 'Dark' }))

    expect(document.documentElement.dataset.theme).toBe('dark')
    vi.restoreAllMocks()
  })
})
