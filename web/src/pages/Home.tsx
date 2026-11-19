import { Navigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { useMyFlats } from '@/api/flats'
import { flatHomePath } from '@/components/layout/navigation'
import { Onboarding } from './Onboarding'

/** Entry point at `/`: sends a member to their flat, or offers onboarding when they have none. */
export function Home() {
  const { t } = useTranslation('home')
  const flats = useMyFlats()

  if (flats.isPending) {
    return <p role="status">{t('loading')}</p>
  }

  if (flats.isError) {
    return (
      <p role="alert" className="text-ink">
        {t('loadError')}
      </p>
    )
  }

  // A member with no flat is offered the onboarding choice instead of an empty home.
  const [flat] = flats.data
  if (flat === undefined) {
    return <Onboarding />
  }

  return <Navigate to={flatHomePath(flat.id)} replace />
}
