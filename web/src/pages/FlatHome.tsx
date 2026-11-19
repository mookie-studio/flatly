import { useId, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import {
  useChangeMemberRole,
  useFlat,
  useFlatMembers,
  useLeaveFlat,
  useRemoveMember,
  type FlatMember,
  type MemberRole,
} from '@/api/flats'
import { isApiError } from '@/api/errors'
import { useSession } from '@/auth/use-session'
import { flatModuleLinks } from '@/components/layout/navigation'

/** An action that waits for the member's confirmation before the API is called. */
type Pending =
  | { kind: 'leave' }
  | { kind: 'remove'; member: FlatMember }
  | { kind: 'demote'; member: FlatMember }

const buttonClass =
  'min-h-11 rounded-md border border-line bg-surface-raised px-3 text-sm font-medium text-ink hover:bg-accent-soft disabled:opacity-60'

/** Home of one flat: its members, their management, and shortcuts to the modules that exist. */
export function FlatHome() {
  const { flatId = '' } = useParams()
  const { t } = useTranslation('home')
  const navigate = useNavigate()
  const session = useSession()
  const flat = useFlat(flatId)
  const members = useFlatMembers(flatId)
  const leave = useLeaveFlat(flatId)
  const changeRole = useChangeMemberRole(flatId)
  const removeMember = useRemoveMember(flatId)
  const [pending, setPending] = useState<Pending | null>(null)

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

  const callerId = session.status === 'authenticated' ? session.userId : null
  const isAdmin = flat.data.role === 'Admin'
  const busy = leave.isPending || changeRole.isPending || removeMember.isPending
  const actionError = leave.error ?? changeRole.error ?? removeMember.error

  const closePending = () => setPending(null)

  const confirmPending = () => {
    if (pending?.kind === 'leave') {
      leave.mutate(undefined, {
        onSuccess: () => navigate('/', { replace: true }),
        onSettled: closePending,
      })
    } else if (pending?.kind === 'remove') {
      removeMember.mutate(pending.member.userId, { onSettled: closePending })
    } else if (pending?.kind === 'demote') {
      changeRole.mutate(
        { userId: pending.member.userId, role: 'Member' },
        { onSettled: closePending },
      )
    }
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
        {actionError && (
          <p role="alert" className="text-ink">
            {isApiError(actionError) && actionError.status === 409
              ? t('members.lastAdmin')
              : t('members.error')}
          </p>
        )}
        <ul
          aria-labelledby="flat-members"
          className="flex flex-col divide-y divide-line rounded-md border border-line bg-surface-raised"
        >
          {members.data.map((member) => {
            const isSelf = member.userId === callerId
            const rowPending =
              pending && pending.kind !== 'leave' && pending.member.userId === member.userId
                ? pending
                : null
            return (
              <MemberRow
                key={member.userId}
                member={member}
                isSelf={isSelf}
                canManage={isAdmin && !isSelf}
                pending={rowPending}
                busy={busy}
                onChangeRole={(role: MemberRole) =>
                  changeRole.mutate({ userId: member.userId, role })
                }
                onAsk={(kind) => setPending({ kind, member })}
                onConfirm={confirmPending}
                onCancel={closePending}
              />
            )
          })}
        </ul>
      </section>

      {callerId && (
        <section aria-label={t('leave.action')} className="flex flex-col gap-3">
          {pending?.kind === 'leave' ? (
            <ConfirmPanel
              message={t('leave.confirm')}
              busy={busy}
              onConfirm={confirmPending}
              onCancel={closePending}
            />
          ) : (
            <div>
              <button
                type="button"
                className={buttonClass}
                disabled={busy}
                onClick={() => setPending({ kind: 'leave' })}
              >
                {t('leave.action')}
              </button>
            </div>
          )}
        </section>
      )}
    </div>
  )
}

interface MemberRowProps {
  member: FlatMember
  isSelf: boolean
  canManage: boolean
  pending: Extract<Pending, { kind: 'remove' | 'demote' }> | null
  busy: boolean
  onChangeRole: (role: MemberRole) => void
  onAsk: (kind: 'remove' | 'demote') => void
  onConfirm: () => void
  onCancel: () => void
}

function MemberRow({
  member,
  isSelf,
  canManage,
  pending,
  busy,
  onChangeRole,
  onAsk,
  onConfirm,
  onCancel,
}: MemberRowProps) {
  const { t } = useTranslation('home')
  const initial = member.displayName.trim().charAt(0).toUpperCase()
  const isAdminMember = member.role === 'Admin'

  return (
    <li className="flex flex-col gap-3 px-4 py-3">
      <div className="flex items-center gap-3">
        {member.avatarUrl ? (
          <img
            src={member.avatarUrl}
            alt=""
            className="size-10 shrink-0 rounded-full object-cover"
          />
        ) : (
          <span
            aria-hidden="true"
            className="flex size-10 shrink-0 items-center justify-center rounded-full bg-accent-soft text-sm font-semibold text-ink"
          >
            {initial}
          </span>
        )}
        <div className="flex min-w-0 flex-col">
          <span className="truncate font-medium text-ink">
            {member.displayName}
            {isSelf && <span className="text-ink-muted"> ({t('members.you')})</span>}
          </span>
          <span className="text-sm text-ink-muted">
            {isAdminMember ? t('members.admin') : t('members.member')}
          </span>
        </div>
      </div>

      {canManage &&
        (pending ? (
          <ConfirmPanel
            message={t(
              pending.kind === 'remove' ? 'members.confirmRemove' : 'members.confirmDemote',
              { name: member.displayName },
            )}
            busy={busy}
            onConfirm={onConfirm}
            onCancel={onCancel}
          />
        ) : (
          <div className="flex flex-wrap gap-2">
            {isAdminMember ? (
              <button
                type="button"
                className={buttonClass}
                disabled={busy}
                onClick={() => onAsk('demote')}
              >
                {t('members.demote')}
              </button>
            ) : (
              <button
                type="button"
                className={buttonClass}
                disabled={busy}
                onClick={() => onChangeRole('Admin')}
              >
                {t('members.promote')}
              </button>
            )}
            <button
              type="button"
              className={buttonClass}
              disabled={busy}
              onClick={() => onAsk('remove')}
            >
              {t('members.remove')}
            </button>
          </div>
        ))}
    </li>
  )
}

interface ConfirmPanelProps {
  message: string
  busy: boolean
  onConfirm: () => void
  onCancel: () => void
}

/** Asks for confirmation before a destructive action. Nothing is sent to the API until Confirm. */
function ConfirmPanel({ message, busy, onConfirm, onCancel }: ConfirmPanelProps) {
  const { t } = useTranslation('home')
  const messageId = useId()

  return (
    <div role="alertdialog" aria-describedby={messageId} className="flex flex-col gap-3">
      <p id={messageId} className="text-ink">
        {message}
      </p>
      <div className="flex flex-wrap gap-2">
        <button type="button" className={buttonClass} disabled={busy} onClick={onConfirm}>
          {t('members.confirm')}
        </button>
        <button type="button" className={buttonClass} disabled={busy} onClick={onCancel}>
          {t('members.cancel')}
        </button>
      </div>
    </div>
  )
}
