import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, Navigate, useSearchParams } from 'react-router'
import { useJoinFlat } from '@/api/flats'
import { isApiError } from '@/api/errors'
import { flatHomePath } from '@/components/layout/navigation'

/** Matches the API: an invite code is exactly 8 characters once trimmed. */
export const INVITE_CODE_LENGTH = 8

const primaryButtonClass =
  'inline-flex min-h-12 w-full items-center justify-center rounded-md bg-accent px-4 text-base font-medium text-accent-ink transition-opacity hover:opacity-90 disabled:opacity-60'

const secondaryButtonClass =
  'inline-flex min-h-12 w-full items-center justify-center rounded-md border border-line bg-surface-raised px-4 text-base font-medium text-ink no-underline transition-colors hover:bg-accent-soft'

/** Codes are case-insensitive and may be pasted with spaces around them. */
export function normalizeInviteCode(value: string): string {
  return value.trim().toUpperCase()
}

function isWellFormedCode(code: string): boolean {
  return code.length === INVITE_CODE_LENGTH
}

type JoinErrorKey = 'notFound' | 'expired' | 'throttled' | 'generic'

/** Maps the API answer to a message: unknown, expired and throttled codes each get their own. */
function joinErrorKey(error: unknown): JoinErrorKey {
  if (!isApiError(error)) return 'generic'
  if (error.status === 404) return 'notFound'
  if (error.status === 410) return 'expired'
  if (error.status === 429) return 'throttled'
  return 'generic'
}

/**
 * Landing for invite links (`/join?code=ABCD2345`) and the code field for members who were given a
 * code. A link joins on arrival, once the visitor has signed in; the route is behind sign-in, so the
 * code survives the trip through the login screen. On success the member lands on the flat.
 */
export function JoinFlat() {
  const { t } = useTranslation('join')
  const [params] = useSearchParams()
  const linkCode = normalizeInviteCode(params.get('code') ?? '')
  const [code, setCode] = useState(linkCode)
  const join = useJoinFlat()
  const { mutate: joinFlat } = join
  const autoJoinStarted = useRef(false)

  // A link with a well-formed code joins once. The ref keeps development double-mounting from sending it twice.
  useEffect(() => {
    if (autoJoinStarted.current || !isWellFormedCode(linkCode)) return
    autoJoinStarted.current = true
    joinFlat(linkCode)
  }, [linkCode, joinFlat])

  if (join.isSuccess) {
    return <Navigate to={flatHomePath(join.data.id)} replace />
  }

  const normalized = normalizeInviteCode(code)
  const canSubmit = isWellFormedCode(normalized) && !join.isPending

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canSubmit) return
    joinFlat(normalized)
  }

  const errorKey = join.isError ? joinErrorKey(join.error) : null

  return (
    <section className="mx-auto flex max-w-sm flex-col gap-6 py-8">
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold text-ink">{t('title')}</h1>
        <p className="text-ink-muted">{t('intro')}</p>
      </div>
      {join.isPending && isWellFormedCode(linkCode) && (
        <p role="status" className="text-ink-muted">
          {t('joining')}
        </p>
      )}
      {errorKey && (
        <div
          role="alert"
          className="flex flex-col gap-1 rounded-md border border-line bg-surface-raised p-4"
        >
          <p className="font-medium text-ink">{t(`errors.${errorKey}.title`)}</p>
          <p className="text-sm text-ink-muted">{t(`errors.${errorKey}.hint`)}</p>
        </div>
      )}
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <label htmlFor="invite-code" className="flex flex-col gap-2 text-sm font-medium text-ink">
          {t('codeLabel')}
          <input
            id="invite-code"
            name="code"
            type="text"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            autoComplete="off"
            autoCapitalize="characters"
            spellCheck={false}
            required
            placeholder={t('codePlaceholder')}
            className="min-h-12 rounded-md border border-line bg-surface-raised px-3 font-mono text-base uppercase text-ink"
          />
        </label>
        <button type="submit" disabled={!canSubmit} className={primaryButtonClass}>
          {join.isPending ? t('submitting') : t('submit')}
        </button>
        <Link to="/" className={secondaryButtonClass}>
          {t('back')}
        </Link>
      </form>
    </section>
  )
}
