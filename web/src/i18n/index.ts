import i18next from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'

import enAuth from './locales/en/auth.json'
import enChores from './locales/en/chores.json'
import enCommon from './locales/en/common.json'
import enHome from './locales/en/home.json'
import enJoin from './locales/en/join.json'
import enNotFound from './locales/en/notFound.json'
import enOnboarding from './locales/en/onboarding.json'
import esAuth from './locales/es/auth.json'
import esChores from './locales/es/chores.json'
import esCommon from './locales/es/common.json'
import esHome from './locales/es/home.json'
import esJoin from './locales/es/join.json'
import esNotFound from './locales/es/notFound.json'
import esOnboarding from './locales/es/onboarding.json'
import enShell from './locales/en/shell.json'
import esShell from './locales/es/shell.json'

export const SUPPORTED_LANGUAGES = ['es', 'en'] as const
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number]

export const LANGUAGE_STORAGE_KEY = 'flatly.language'

// Translations are grouped per feature (namespace). Add a feature by adding a
// JSON file for each language and listing it here.
export const resources = {
  es: {
    common: esCommon,
    auth: esAuth,
    chores: esChores,
    home: esHome,
    join: esJoin,
    notFound: esNotFound,
    onboarding: esOnboarding,
    shell: esShell,
  },
  en: {
    common: enCommon,
    auth: enAuth,
    chores: enChores,
    home: enHome,
    join: enJoin,
    notFound: enNotFound,
    onboarding: enOnboarding,
    shell: enShell,
  },
} as const

export const defaultNS = 'common'

// Language detection: a previously chosen language (localStorage), then the
// browser language. A profile preference joins this chain once accounts exist.
// Spanish is the fallback because it is the product's primary language.
export const i18n = i18next.createInstance()

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    ns: ['common', 'auth', 'home', 'join', 'notFound', 'onboarding', 'shell'],
    defaultNS,
    supportedLngs: SUPPORTED_LANGUAGES,
    nonExplicitSupportedLngs: true,
    load: 'languageOnly',
    fallbackLng: 'es',
    interpolation: { escapeValue: false },
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: LANGUAGE_STORAGE_KEY,
      caches: ['localStorage'],
    },
  })

// Keep the document language in sync so screen readers pick the right voice.
i18n.on('languageChanged', (language) => {
  document.documentElement.lang = language
})
