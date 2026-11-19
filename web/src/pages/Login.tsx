import { useTranslation } from 'react-i18next'

export function Login() {
  const { t } = useTranslation('auth')
  return <h1>{t('login.title')}</h1>
}
