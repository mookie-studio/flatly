import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useCreateFlat } from '@/api/flats'

/** Matches the API limit: a flat name is at most 80 characters after trimming. */
export const FLAT_NAME_MAX_LENGTH = 80

const primaryButtonClass =
  'inline-flex min-h-12 w-full items-center justify-center rounded-md bg-accent px-4 text-base font-medium text-accent-ink transition-opacity hover:opacity-90 disabled:opacity-60'

const secondaryButtonClass =
  'inline-flex min-h-12 w-full items-center justify-center rounded-md border border-line bg-surface-raised px-4 text-base font-medium text-ink transition-colors hover:bg-accent-soft disabled:opacity-60 disabled:hover:bg-surface-raised'

/**
 * Shown to a signed-in member who belongs to no flat yet: a choice between creating a flat and
 * joining one. Joining by code is not built yet, so its option is shown but unavailable.
 */
export function Onboarding() {
  const { t } = useTranslation('onboarding')
  const [step, setStep] = useState<'choose' | 'create'>('choose')

  if (step === 'create') {
    return <CreateFlatForm onCancel={() => setStep('choose')} />
  }

  return (
    <section className="mx-auto flex max-w-sm flex-col gap-6 py-8">
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold text-ink">{t('choose.title')}</h1>
        <p className="text-ink-muted">{t('choose.intro')}</p>
      </div>
      <div className="flex flex-col gap-3">
        <button type="button" className={primaryButtonClass} onClick={() => setStep('create')}>
          {t('choose.create')}
        </button>
        <button type="button" className={secondaryButtonClass} disabled>
          {t('choose.join')}
        </button>
        <p className="text-sm text-ink-muted">{t('choose.joinUnavailable')}</p>
      </div>
    </section>
  )
}

function CreateFlatForm({ onCancel }: { onCancel: () => void }) {
  const { t } = useTranslation('onboarding')
  const [name, setName] = useState('')
  const createFlat = useCreateFlat()
  const trimmedName = name.trim()
  const canSubmit = trimmedName.length > 0 && !createFlat.isPending

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canSubmit) return
    createFlat.mutate(trimmedName)
  }

  return (
    <section className="mx-auto flex max-w-sm flex-col gap-6 py-8">
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold text-ink">{t('create.title')}</h1>
        <p className="text-ink-muted">{t('create.intro')}</p>
      </div>
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <label htmlFor="flat-name" className="flex flex-col gap-2 text-sm font-medium text-ink">
          {t('create.nameLabel')}
          <input
            id="flat-name"
            name="name"
            type="text"
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={FLAT_NAME_MAX_LENGTH}
            autoComplete="off"
            required
            placeholder={t('create.namePlaceholder')}
            className="min-h-12 rounded-md border border-line bg-surface-raised px-3 text-base font-normal text-ink"
          />
        </label>
        {createFlat.isError && (
          <p role="alert" className="text-sm text-ink">
            {t('create.error')}
          </p>
        )}
        <button type="submit" disabled={!canSubmit} className={primaryButtonClass}>
          {createFlat.isPending ? t('create.submitting') : t('create.submit')}
        </button>
        <button type="button" className={secondaryButtonClass} onClick={onCancel}>
          {t('create.back')}
        </button>
      </form>
    </section>
  )
}
