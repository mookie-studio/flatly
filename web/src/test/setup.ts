import '@testing-library/jest-dom/vitest'
import { afterAll, afterEach, beforeAll, beforeEach } from 'vitest'
import { i18n } from '@/i18n'
import { server } from './server'

// Tests assert on English copy unless they choose a language themselves.
beforeEach(async () => {
  await i18n.changeLanguage('en')
})

// Any request without a mocked response fails the test instead of reaching a real API.
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
