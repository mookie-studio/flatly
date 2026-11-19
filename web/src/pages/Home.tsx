import { useTranslation } from 'react-i18next'
import { useMyFlats } from '@/api/flats'
import { Onboarding } from './Onboarding'

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

  return (
    <section className="flex flex-col gap-1">
      <p className="text-sm text-ink-muted">{t('title')}</p>
      <h1 className="text-2xl font-semibold text-ink">{flat.name}</h1>
    </section>
  )
}
