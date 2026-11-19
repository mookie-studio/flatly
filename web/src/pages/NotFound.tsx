import { useTranslation } from 'react-i18next'

export function NotFound() {
  const { t } = useTranslation('notFound')
  return <h1>{t('title')}</h1>
}
