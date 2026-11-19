import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'

export function BrandLink() {
  const { t } = useTranslation('shell')
  return (
    <Link to="/" className="rounded-md text-lg font-semibold tracking-tight text-ink no-underline">
      {t('brand')}
    </Link>
  )
}
