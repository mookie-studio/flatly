import { useTranslation } from 'react-i18next'
import { useMatch, useNavigate } from 'react-router'
import { useMyFlats } from '@/api/flats'
import { flatHomePath } from './navigation'

/**
 * Lets a member who belongs to several flats move between them. Members with one flat, or none,
 * see no switcher. The active flat is read from the URL, so switching changes the data shown.
 */
export function FlatSwitcher() {
  const { t } = useTranslation('shell')
  const navigate = useNavigate()
  const match = useMatch('/f/:flatId/*')
  const flats = useMyFlats()

  if (!flats.isSuccess || flats.data.length < 2) {
    return null
  }

  const currentId = match?.params.flatId ?? ''

  return (
    <select
      aria-label={t('flatSwitcher.label')}
      value={currentId}
      onChange={(event) => navigate(flatHomePath(event.target.value))}
      className="min-h-9 max-w-48 truncate rounded-md border border-line bg-surface-raised px-2 text-sm font-medium text-ink"
    >
      {currentId === '' && (
        <option value="" disabled>
          {t('flatSwitcher.placeholder')}
        </option>
      )}
      {flats.data.map((flat) => (
        <option key={flat.id} value={flat.id}>
          {flat.name}
        </option>
      ))}
    </select>
  )
}
