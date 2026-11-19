import { useTranslation } from 'react-i18next'
import { Navigate, useSearchParams } from 'react-router'
import { useAuth } from '@/auth/auth-context'
import { signInUrl } from '@/auth/sign-in'
import { safeRedirectPath } from '@/lib/redirect'

const buttonClass =
  'inline-flex min-h-12 w-full items-center justify-center rounded-md border border-line bg-surface-raised px-4 text-base font-medium text-ink no-underline transition-colors hover:bg-accent-soft'

export function Login() {
  const { t } = useTranslation('auth')
  const auth = useAuth()
  const [params] = useSearchParams()
  const returnPath = safeRedirectPath(params.get('redirect'))

  // Already signed in (for example, from another tab): skip the form and go where the visitor was headed.
  if (auth.status === 'authenticated') {
    return <Navigate to={returnPath} replace />
  }

  return (
    <section className="mx-auto flex max-w-sm flex-col gap-6 py-8">
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold text-ink">{t('login.title')}</h1>
        <p className="text-ink-muted">{t('login.intro')}</p>
      </div>
      <div className="flex flex-col gap-3">
        <a href={signInUrl('google', returnPath)} className={buttonClass}>
          {t('login.google')}
        </a>
        <a href={signInUrl('microsoft', returnPath)} className={buttonClass}>
          {t('login.microsoft')}
        </a>
      </div>
    </section>
  )
}
