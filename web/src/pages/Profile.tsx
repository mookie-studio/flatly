import { useState, useId, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import {
  useDeleteAccount,
  useExportAccount,
  useProfile,
  useUpdateProfile,
  type Profile as ProfileData,
  type ProfileUpdate,
} from '@/api/account'
import { isApiError } from '@/api/errors'
import { i18n, SUPPORTED_LANGUAGES, type SupportedLanguage } from '@/i18n'

/** Matches the API limits. */
export const DISPLAY_NAME_MAX_LENGTH = 80
export const AVATAR_URL_MAX_LENGTH = 2048

const buttonClass =
  'inline-flex min-h-12 w-full items-center justify-center rounded-md bg-accent px-4 text-base font-medium text-accent-ink transition-opacity hover:opacity-90 disabled:opacity-60 sm:w-auto'

const secondaryButtonClass =
  'inline-flex min-h-12 w-full items-center justify-center rounded-md border border-line bg-surface-raised px-4 text-base font-medium text-ink transition-colors hover:bg-accent-soft disabled:opacity-60 sm:w-auto'

const inputClass =
  'min-h-12 rounded-md border border-line bg-surface-raised px-3 text-base font-normal text-ink'

/** A secure (https) absolute URL, as the API requires for the avatar. */
export function isSecureUrl(value: string): boolean {
  try {
    return new URL(value).protocol === 'https:'
  } catch {
    return false
  }
}

/** Every IANA zone the browser knows, with the member's current zone kept even if the browser lacks it. */
export function timeZoneOptions(current: string): string[] {
  const zones = Intl.supportedValuesOf('timeZone')
  return zones.includes(current) ? zones : [current, ...zones]
}

/** The `detail` text of an RFC 9457 problem body, when the API sent one. */
function problemDetail(problem: unknown): string | undefined {
  if (typeof problem === 'object' && problem !== null && 'detail' in problem) {
    return typeof problem.detail === 'string' ? problem.detail : undefined
  }
  return undefined
}

/** The member's profile, data export and account deletion, in one settings screen. */
export function Profile() {
  const { t } = useTranslation(['profile', 'common'])
  const profile = useProfile()

  return (
    <div className="flex flex-col gap-10">
      <header className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold text-ink">{t('profile:title')}</h1>
        <p className="text-ink-muted">{t('profile:intro')}</p>
      </header>

      {profile.isPending && <p role="status">{t('status.loading', { ns: 'common' })}</p>}
      {profile.isError && (
        <p role="alert" className="text-sm text-ink">
          {t('profile:loadError')}
        </p>
      )}
      {profile.data && <ProfileForm profile={profile.data} />}

      <DataExport />
      <DeleteAccount />
    </div>
  )
}

function ProfileForm({ profile }: { profile: ProfileData }) {
  const { t } = useTranslation(['profile', 'common'])
  const update = useUpdateProfile()
  const [displayName, setDisplayName] = useState(profile.displayName)
  const [avatarUrl, setAvatarUrl] = useState(profile.avatarUrl ?? '')
  const [language, setLanguage] = useState<string>(profile.preferredLanguage)
  const [timeZoneId, setTimeZoneId] = useState(profile.timeZoneId)
  const [saved, setSaved] = useState(false)

  const trimmedName = displayName.trim()
  const trimmedAvatar = avatarUrl.trim()
  const avatarValid =
    trimmedAvatar === '' ||
    (trimmedAvatar.length <= AVATAR_URL_MAX_LENGTH && isSecureUrl(trimmedAvatar))
  const canSubmit = trimmedName.length > 0 && avatarValid && !update.isPending
  const zones = timeZoneOptions(profile.timeZoneId)

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canSubmit) return
    setSaved(false)
    // Every field is sent, because the API replaces all of them.
    const body: ProfileUpdate = {
      displayName: trimmedName,
      avatarUrl: trimmedAvatar === '' ? null : trimmedAvatar,
      preferredLanguage: language,
      timeZoneId,
    }
    update.mutate(body, {
      onSuccess: (result) => {
        // The interface switches language straight away, without a reload.
        void i18n.changeLanguage(result.preferredLanguage)
        setSaved(true)
      },
    })
  }

  return (
    <form onSubmit={handleSubmit} className="flex max-w-md flex-col gap-5">
      <label
        htmlFor="profile-display-name"
        className="flex flex-col gap-2 text-sm font-medium text-ink"
      >
        {t('profile:form.displayName')}
        <input
          id="profile-display-name"
          name="displayName"
          type="text"
          value={displayName}
          onChange={(event) => setDisplayName(event.target.value)}
          maxLength={DISPLAY_NAME_MAX_LENGTH}
          autoComplete="name"
          required
          className={inputClass}
        />
      </label>
      {trimmedName.length === 0 && (
        <p className="-mt-3 text-sm text-ink-muted">{t('profile:form.displayNameRequired')}</p>
      )}

      <label htmlFor="profile-avatar" className="flex flex-col gap-2 text-sm font-medium text-ink">
        {t('profile:form.avatarUrl')}
        <input
          id="profile-avatar"
          name="avatarUrl"
          type="url"
          value={avatarUrl}
          onChange={(event) => setAvatarUrl(event.target.value)}
          autoComplete="url"
          inputMode="url"
          aria-invalid={!avatarValid}
          className={inputClass}
        />
      </label>
      {avatarValid ? (
        <p className="-mt-3 text-sm text-ink-muted">{t('profile:form.avatarHint')}</p>
      ) : (
        <p role="alert" className="-mt-3 text-sm text-ink">
          {t('profile:form.avatarInvalid')}
        </p>
      )}

      <LanguageField value={language} onChange={setLanguage} />

      <label
        htmlFor="profile-time-zone"
        className="flex flex-col gap-2 text-sm font-medium text-ink"
      >
        {t('profile:form.timeZone')}
        <select
          id="profile-time-zone"
          name="timeZoneId"
          value={timeZoneId}
          onChange={(event) => setTimeZoneId(event.target.value)}
          className={inputClass}
        >
          {zones.map((zone) => (
            <option key={zone} value={zone}>
              {zone}
            </option>
          ))}
        </select>
      </label>

      {update.isError && (
        <p role="alert" className="text-sm text-ink">
          {t('profile:form.saveError')}
        </p>
      )}
      {saved && !update.isPending && (
        <p role="status" className="text-sm text-ink">
          {t('profile:form.saved')}
        </p>
      )}

      <button type="submit" disabled={!canSubmit} className={buttonClass}>
        {update.isPending ? t('profile:form.saving') : t('profile:form.save')}
      </button>
    </form>
  )
}

function LanguageField({
  value,
  onChange,
}: {
  value: string
  onChange: (language: string) => void
}) {
  const { t } = useTranslation(['profile', 'common'])
  const id = useId()

  return (
    <label htmlFor={id} className="flex flex-col gap-2 text-sm font-medium text-ink">
      {t('profile:form.language')}
      <select
        id={id}
        name="preferredLanguage"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className={inputClass}
      >
        {SUPPORTED_LANGUAGES.map((language: SupportedLanguage) => (
          <option key={language} value={language}>
            {t(`language.${language}`, { ns: 'common' })}
          </option>
        ))}
      </select>
    </label>
  )
}

function DataExport() {
  const { t } = useTranslation('profile')
  const exportAccount = useExportAccount()

  return (
    <section
      aria-labelledby="export-heading"
      className="flex max-w-md flex-col gap-3 border-t border-line pt-8"
    >
      <h2 id="export-heading" className="text-xl font-semibold text-ink">
        {t('export.title')}
      </h2>
      <p className="text-ink-muted">{t('export.intro')}</p>
      <button
        type="button"
        onClick={() => exportAccount.mutate()}
        disabled={exportAccount.isPending}
        className={secondaryButtonClass}
      >
        {exportAccount.isPending ? t('export.preparing') : t('export.action')}
      </button>
      {exportAccount.isError && (
        <p role="alert" className="text-sm text-ink">
          {t('export.error')}
        </p>
      )}
    </section>
  )
}

function DeleteAccount() {
  const { t } = useTranslation('profile')
  const navigate = useNavigate()
  const deleteAccount = useDeleteAccount()
  const [confirming, setConfirming] = useState(false)
  const headingId = useId()

  const error = deleteAccount.error
  const lastAdmin = isApiError(error) && error.status === 409
  const detail = lastAdmin ? problemDetail(error.problem) : undefined

  function handleConfirm() {
    deleteAccount.mutate(undefined, {
      onSuccess: () => navigate('/login', { replace: true }),
    })
  }

  return (
    <section
      aria-labelledby={headingId}
      className="flex max-w-md flex-col gap-3 border-t border-line pt-8"
    >
      <h2 id={headingId} className="text-xl font-semibold text-ink">
        {t('delete.title')}
      </h2>
      <p className="text-ink-muted">{t('delete.intro')}</p>

      {!confirming && (
        <button type="button" onClick={() => setConfirming(true)} className={secondaryButtonClass}>
          {t('delete.action')}
        </button>
      )}

      {confirming && (
        <div
          role="alertdialog"
          aria-labelledby={`${headingId}-confirm`}
          className="flex flex-col gap-3 rounded-md border border-line bg-surface-raised p-4"
        >
          <h3 id={`${headingId}-confirm`} className="font-semibold text-ink">
            {t('delete.confirmTitle')}
          </h3>
          <p className="text-sm text-ink-muted">{t('delete.confirmBody')}</p>
          <button
            type="button"
            onClick={handleConfirm}
            disabled={deleteAccount.isPending}
            className={buttonClass}
          >
            {deleteAccount.isPending ? t('delete.deleting') : t('delete.confirm')}
          </button>
          <button
            type="button"
            onClick={() => setConfirming(false)}
            disabled={deleteAccount.isPending}
            className={secondaryButtonClass}
          >
            {t('delete.cancel')}
          </button>
        </div>
      )}

      {lastAdmin && (
        <div role="alert" className="flex flex-col gap-2 text-sm text-ink">
          <p>{t('delete.lastAdmin')}</p>
          {detail && <p className="font-medium">{detail}</p>}
        </div>
      )}
      {deleteAccount.isError && !lastAdmin && (
        <p role="alert" className="text-sm text-ink">
          {t('delete.error')}
        </p>
      )}
    </section>
  )
}
