import { useTranslation } from 'react-i18next'
import { useSignOut } from '@/auth/use-session'

export function SignOutButton() {
  const { t } = useTranslation('shell')
  const signOut = useSignOut()

  return (
    <div className="flex flex-col items-end gap-1">
      <button
        type="button"
        onClick={() => signOut.mutate()}
        disabled={signOut.isPending}
        className="inline-flex min-h-9 items-center rounded-md px-3 text-sm font-medium text-ink-muted transition-colors hover:text-ink disabled:opacity-60"
      >
        {t('signOut.action')}
      </button>
      {signOut.isError && (
        <p role="alert" className="text-sm text-ink">
          {t('signOut.error')}
        </p>
      )}
    </div>
  )
}
