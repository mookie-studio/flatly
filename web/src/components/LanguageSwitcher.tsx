import { useId } from 'react'
import { useTranslation } from 'react-i18next'
import { SUPPORTED_LANGUAGES, type SupportedLanguage } from '@/i18n'

export function LanguageSwitcher() {
  const { t, i18n } = useTranslation('common')
  const selectId = useId()
  const current = (i18n.resolvedLanguage ?? i18n.language) as SupportedLanguage

  return (
    <div>
      <label htmlFor={selectId}>{t('language.label')}</label>
      <select
        id={selectId}
        value={current}
        onChange={(event) => void i18n.changeLanguage(event.target.value)}
      >
        {SUPPORTED_LANGUAGES.map((language) => (
          <option key={language} value={language}>
            {t(`language.${language}`)}
          </option>
        ))}
      </select>
    </div>
  )
}
