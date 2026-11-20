import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { useChores, useCreateChore, useUpdateChore, type Chore } from '@/api/chores'
import { isApiError } from '@/api/errors'
import { useFlatMembers, type FlatMember } from '@/api/flats'
import { flatHomePath } from '@/components/layout/navigation'
import {
  RECURRENCE_EVERY_MAX,
  RECURRENCE_EVERY_MIN,
  RECURRENCE_UNITS,
  WEIGHT_MAX,
  WEIGHT_MIN,
  isChoreField,
  toChorePayload,
  validateChore,
  type ChoreErrors,
  type ChoreValues,
  type RecurrenceUnit,
} from '@/lib/chore-rules'

const inputClass =
  'min-h-12 rounded-md border border-line bg-surface-raised px-3 text-base text-ink disabled:opacity-60'

const buttonClass =
  'inline-flex min-h-12 items-center justify-center rounded-md border border-line bg-surface-raised px-4 text-base font-medium text-ink hover:bg-accent-soft disabled:opacity-60'

const primaryButtonClass =
  'inline-flex min-h-12 items-center justify-center rounded-md bg-accent px-4 text-base font-medium text-accent-ink transition-opacity hover:opacity-90 disabled:opacity-60'

/**
 * Creates a chore, or edits one and deactivates it. The form checks what the API checks before it
 * sends anything; API validation answers stay next to the field they belong to.
 */
export function ChoreEditor() {
  const { flatId = '', choreId } = useParams()
  const { t } = useTranslation('chores')
  const members = useFlatMembers(flatId)
  const chores = useChores(flatId, true, choreId !== undefined)

  if (members.isPending || (choreId !== undefined && chores.isPending)) {
    return <p role="status">{t('loading')}</p>
  }

  if (members.isError || chores.isError) {
    const error = members.error ?? chores.error
    // 403 and 404 both mean the caller cannot see this flat, so the message does not reveal whether it exists.
    if (isApiError(error) && (error.status === 403 || error.status === 404)) {
      return <p role="alert">{t('notFound')}</p>
    }
    return <p role="alert">{t('loadError')}</p>
  }

  const chore =
    choreId === undefined ? null : (chores.data?.find((item) => item.id === choreId) ?? null)
  if (choreId !== undefined && chore === null) {
    return <p role="alert">{t('notFound')}</p>
  }

  return (
    <section className="mx-auto flex max-w-xl flex-col gap-6 py-4">
      <h1 className="text-2xl font-semibold text-ink">{t(chore ? 'editTitle' : 'newTitle')}</h1>
      <ChoreForm key={chore?.id ?? 'new'} flatId={flatId} chore={chore} members={members.data} />
    </section>
  )
}

interface ChoreFormProps {
  flatId: string
  chore: Chore | null
  members: FlatMember[]
}

function ChoreForm({ flatId, chore, members }: ChoreFormProps) {
  const { t } = useTranslation('chores')
  const navigate = useNavigate()
  const create = useCreateChore(flatId)
  const update = useUpdateChore(flatId)
  const [values, setValues] = useState<ChoreValues>(() =>
    chore ? valuesFromChore(chore) : emptyValues(members),
  )
  const [errors, setErrors] = useState<ChoreErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [confirmingDeactivate, setConfirmingDeactivate] = useState(false)

  const busy = create.isPending || update.isPending

  const setField = <K extends keyof ChoreValues>(key: K, value: ChoreValues[K]) =>
    setValues((current) => ({ ...current, [key]: value }))

  const toggleParticipant = (userId: string, checked: boolean) =>
    setField(
      'participantIds',
      checked
        ? [...values.participantIds, userId]
        : values.participantIds.filter((id) => id !== userId),
    )

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy) return

    const found = validateChore(values)
    setErrors(found)
    setFormError(null)
    if (Object.keys(found).length > 0) return

    const onError = (error: unknown) => {
      const apiErrors = errorsFromApi(error)
      setErrors(apiErrors)
      setFormError(Object.keys(apiErrors).length > 0 ? null : t('saveError'))
    }

    if (chore) {
      update.mutate(
        { choreId: chore.id, changes: toChorePayload(values) },
        { onSuccess: () => navigate(flatHomePath(flatId)), onError },
      )
    } else {
      create.mutate(toChorePayload(values), {
        onSuccess: () => navigate(flatHomePath(flatId)),
        onError,
      })
    }
  }

  function changeActive(isActive: boolean) {
    if (!chore) return
    setFormError(null)
    update.mutate(
      { choreId: chore.id, changes: { isActive } },
      {
        onSuccess: () => setConfirmingDeactivate(false),
        onError: () => {
          setConfirmingDeactivate(false)
          setFormError(t('changeError'))
        },
      },
    )
  }

  const describe = (field: string) =>
    errors[field as keyof ChoreErrors] ? `${field}-error` : undefined

  return (
    <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-6">
      {formError && (
        <p role="alert" className="text-ink">
          {formError}
        </p>
      )}

      <div className="flex flex-col gap-2">
        <label htmlFor="chore-name" className="text-sm font-medium text-ink">
          {t('fields.name')}
        </label>
        <input
          id="chore-name"
          name="name"
          type="text"
          value={values.name}
          onChange={(event) => setField('name', event.target.value)}
          aria-invalid={errors.name ? true : undefined}
          aria-describedby={describe('name')}
          disabled={busy}
          className={inputClass}
        />
        <FieldError id="name-error" message={errors.name && t(`validation.${errors.name}`)} />
      </div>

      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm font-medium text-ink">{t('fields.recurrence')}</legend>
        <div className="flex flex-wrap items-start gap-3">
          <div className="flex flex-col gap-2">
            <label htmlFor="chore-every" className="text-sm text-ink-muted">
              {t('fields.every')}
            </label>
            <input
              id="chore-every"
              name="recurrenceEvery"
              type="number"
              inputMode="numeric"
              min={RECURRENCE_EVERY_MIN}
              max={RECURRENCE_EVERY_MAX}
              step={1}
              value={values.recurrenceEvery}
              onChange={(event) => setField('recurrenceEvery', event.target.value)}
              aria-invalid={errors.recurrenceEvery ? true : undefined}
              aria-describedby={describe('recurrenceEvery')}
              disabled={busy}
              className={`${inputClass} w-24`}
            />
          </div>
          <div className="flex flex-col gap-2">
            <label htmlFor="chore-unit" className="text-sm text-ink-muted">
              {t('fields.unit')}
            </label>
            <select
              id="chore-unit"
              name="recurrenceUnit"
              value={values.recurrenceUnit}
              onChange={(event) => setField('recurrenceUnit', event.target.value as RecurrenceUnit)}
              disabled={busy}
              className={inputClass}
            >
              {RECURRENCE_UNITS.map((unit) => (
                <option key={unit} value={unit}>
                  {t(`units.${unit}`)}
                </option>
              ))}
            </select>
          </div>
        </div>
        <FieldError
          id="recurrenceEvery-error"
          message={errors.recurrenceEvery && t(`validation.${errors.recurrenceEvery}`)}
        />
        <FieldError
          id="recurrenceUnit-error"
          message={errors.recurrenceUnit && t(`validation.${errors.recurrenceUnit}`)}
        />
      </fieldset>

      <div className="flex flex-col gap-2">
        <label htmlFor="chore-weight" className="text-sm font-medium text-ink">
          {t('fields.weight')}
        </label>
        <input
          id="chore-weight"
          name="weight"
          type="number"
          inputMode="numeric"
          min={WEIGHT_MIN}
          max={WEIGHT_MAX}
          step={1}
          value={values.weight}
          onChange={(event) => setField('weight', event.target.value)}
          aria-invalid={errors.weight ? true : undefined}
          aria-describedby={describe('weight')}
          disabled={busy}
          className={`${inputClass} w-24`}
        />
        <FieldError id="weight-error" message={errors.weight && t(`validation.${errors.weight}`)} />
      </div>

      <fieldset className="flex flex-col gap-2" aria-describedby={describe('participantIds')}>
        <legend className="text-sm font-medium text-ink">{t('fields.participants')}</legend>
        <ul className="flex flex-col divide-y divide-line rounded-md border border-line bg-surface-raised">
          {members.map((member) => (
            <li key={member.userId}>
              <label className="flex min-h-12 items-center gap-3 px-4 text-ink">
                <input
                  type="checkbox"
                  name="participantIds"
                  value={member.userId}
                  checked={values.participantIds.includes(member.userId)}
                  onChange={(event) => toggleParticipant(member.userId, event.target.checked)}
                  disabled={busy}
                  className="size-5"
                />
                {member.displayName}
              </label>
            </li>
          ))}
        </ul>
        <FieldError
          id="participantIds-error"
          message={errors.participantIds && t(`validation.${errors.participantIds}`)}
        />
      </fieldset>

      <div className="flex flex-col gap-3">
        <button type="submit" disabled={busy} className={primaryButtonClass}>
          {busy ? t('actions.saving') : t(chore ? 'actions.save' : 'actions.create')}
        </button>
        <Link to={flatHomePath(flatId)} className={buttonClass}>
          {t('actions.back')}
        </Link>
      </div>

      {chore && (
        <section className="flex flex-col gap-3 border-t border-line pt-6">
          {!chore.isActive && <p className="text-ink-muted">{t('status.inactive')}</p>}
          {chore.isActive ? (
            confirmingDeactivate ? (
              <div role="alertdialog" className="flex flex-col gap-3">
                <p className="text-ink">{t('actions.confirmDeactivate')}</p>
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    className={buttonClass}
                    disabled={busy}
                    onClick={() => changeActive(false)}
                  >
                    {t('actions.confirm')}
                  </button>
                  <button
                    type="button"
                    className={buttonClass}
                    disabled={busy}
                    onClick={() => setConfirmingDeactivate(false)}
                  >
                    {t('actions.cancel')}
                  </button>
                </div>
              </div>
            ) : (
              <div>
                <button
                  type="button"
                  className={buttonClass}
                  disabled={busy}
                  onClick={() => setConfirmingDeactivate(true)}
                >
                  {t('actions.deactivate')}
                </button>
              </div>
            )
          ) : (
            <div>
              <button
                type="button"
                className={buttonClass}
                disabled={busy}
                onClick={() => changeActive(true)}
              >
                {t('actions.activate')}
              </button>
            </div>
          )}
        </section>
      )}
    </form>
  )
}

function FieldError({ id, message }: { id: string; message: string | false | null | undefined }) {
  if (!message) return null
  return (
    <p id={id} className="text-sm text-ink">
      {message}
    </p>
  )
}

function emptyValues(members: FlatMember[]): ChoreValues {
  return {
    name: '',
    recurrenceUnit: 'Week',
    recurrenceEvery: '1',
    weight: '1',
    participantIds: members.map((member) => member.userId),
  }
}

function valuesFromChore(chore: Chore): ChoreValues {
  return {
    name: chore.name,
    recurrenceUnit: chore.recurrenceUnit as RecurrenceUnit,
    recurrenceEvery: String(chore.recurrenceEvery),
    weight: String(chore.weight),
    participantIds: chore.participantIds,
  }
}

/**
 * Reads the field errors of a 400 answer (RFC 9457 `errors`, keyed by camelCase field). Participant
 * errors get their own message; any other field gets a generic one. Unknown keys yield nothing, so
 * the caller shows the general message.
 */
function errorsFromApi(error: unknown): ChoreErrors {
  if (!isApiError(error) || error.status !== 400) return {}
  const problem = error.problem as { errors?: Record<string, string[]> } | undefined
  const result: ChoreErrors = {}
  for (const key of Object.keys(problem?.errors ?? {})) {
    if (!isChoreField(key)) continue
    result[key] = key === 'participantIds' ? 'participantsInvalid' : 'fieldInvalid'
  }
  return result
}
