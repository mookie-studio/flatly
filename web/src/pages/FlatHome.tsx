import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { useFlat, useFlatMembers, type FlatMember } from '@/api/flats'
import { isApiError } from '@/api/errors'
import { flatModuleLinks } from '@/components/layout/navigation'
import { rememberLastFlatId } from '@/lib/last-flat'

/** Home of one flat: its members and shortcuts to the modules that exist. */
export function FlatHome() {
  const { flatId = '' } = useParams()
  const { t } = useTranslation('home')
  const flat = useFlat(flatId)
  const members = useFlatMembers(flatId)
  const flatIsVisible = flat.isSuccess

  // Only a flat the member can see is remembered, so the next visit to / lands here.
  useEffect(() => {
    if (flatIsVisible) rememberLastFlatId(flatId)
  }, [flatIsVisible, flatId])

  if (flat.isPending || members.isPending) {
    return <p role="status">{t('loading')}</p>
  }

  if (flat.isError || members.isError) {
    // 403 and 404 both mean the caller cannot see this flat, so the message does not reveal whether it exists.
    const error = flat.error ?? members.error
    if (isApiError(error) && (error.status === 403 || error.status === 404)) {
      return (
        <p role="alert" className="text-ink">
          {t('notFound')}
        </p>
      )
    }
    return (
      <p role="alert" className="text-ink">
        {t('loadError')}
      </p>
    )
  }

  return (
    <div className="flex flex-col gap-8">
      <section className="flex flex-col gap-1">
        <p className="text-sm text-ink-muted">{t('title')}</p>
        <h1 className="text-2xl font-semibold text-ink">{flat.data.name}</h1>
      </section>

      {flatModuleLinks.length > 0 && (
        <section aria-labelledby="flat-shortcuts" className="flex flex-col gap-3">
          <h2 id="flat-shortcuts" className="text-lg font-semibold text-ink">
            {t('shortcuts.heading')}
          </h2>
          <ul className="grid gap-3 sm:grid-cols-2">
            {flatModuleLinks.map((module) => (
              <li key={module.labelKey}>
                <Link
                  to={module.path(flatId)}
                  className="flex min-h-12 items-center rounded-md border border-line bg-surface-raised px-4 text-base font-medium text-ink no-underline hover:bg-accent-soft"
                >
                  {t(module.labelKey)}
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section aria-labelledby="flat-members" className="flex flex-col gap-3">
        <h2 id="flat-members" className="text-lg font-semibold text-ink">
          {t('members.heading')}
        </h2>
        <ul
          aria-labelledby="flat-members"
          className="flex flex-col divide-y divide-line rounded-md border border-line bg-surface-raised"
        >
          {members.data.map((member) => (
            <MemberRow key={member.userId} member={member} />
          ))}
        </ul>
      </section>
    </div>
  )
}

function MemberRow({ member }: { member: FlatMember }) {
  const { t } = useTranslation('home')
  const initial = member.displayName.trim().charAt(0).toUpperCase()

  return (
    <li className="flex items-center gap-3 px-4 py-3">
      {member.avatarUrl ? (
        <img src={member.avatarUrl} alt="" className="size-10 shrink-0 rounded-full object-cover" />
      ) : (
        <span
          aria-hidden="true"
          className="flex size-10 shrink-0 items-center justify-center rounded-full bg-accent-soft text-sm font-semibold text-ink"
        >
          {initial}
        </span>
      )}
      <div className="flex min-w-0 flex-col">
        <span className="truncate font-medium text-ink">{member.displayName}</span>
        <span className="text-sm text-ink-muted">
          {member.role === 'Admin' ? t('members.admin') : t('members.member')}
        </span>
      </div>
    </li>
  )
}
