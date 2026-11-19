import { useTranslation } from 'react-i18next'

export function Home() {
  const { t } = useTranslation('home')
  return <h1>{t('title')}</h1>
}
