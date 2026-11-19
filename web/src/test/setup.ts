import '@testing-library/jest-dom/vitest'
import { beforeEach } from 'vitest'
import { i18n } from '@/i18n'

// Tests assert on English copy unless they choose a language themselves.
beforeEach(async () => {
  await i18n.changeLanguage('en')
})
