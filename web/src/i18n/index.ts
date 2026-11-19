import i18next from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'

import enAuth from './locales/en/auth.json'
import enCommon from './locales/en/common.json'
import enHome from './locales/en/home.json'
import enNotFound from './locales/en/notFound.json'
import esAuth from './locales/es/auth.json'
import esCommon from './locales/es/common.json'
import esHome from './locales/es/home.json'
import esNotFound from './locales/es/notFound.json'

export const SUPPORTED_LANGUAGES = ['es', 'en'] as const
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number]

export const LANGUAGE_STORAGE_KEY = 'flatly.language'

// Translations are grouped per feature (namespace). Add a feature by adding a
// JSON file for each language and listing it here.
export const resources = {
  es: { common: esCommon, auth: esAuth, home: esHome, notFound: esNotFound },
  en: { common: enCommon, auth: enAuth, home: enHome, notFound: enNotFound },
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
    ns: ['common', 'auth', 'home', 'notFound'],
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
