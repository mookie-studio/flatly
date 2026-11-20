import { useId, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useFlatInviteCode, useRegenerateFlatInviteCode } from '@/api/flats'
import { buildInviteLink, whatsappShareHref } from '@/lib/invite-link'

const buttonClass =
  'min-h-11 rounded-md border border-line bg-surface-raised px-3 text-sm font-medium text-ink no-underline hover:bg-accent-soft disabled:opacity-60'

type CopyState = 'idle' | 'copied' | 'failed'

/**
 * Lets an admin share a flat's invite code: copy the link, share it through the device's sharing
 * sheet where the browser offers one, send it on WhatsApp, or replace the code with a new one.
 */
export function InviteSharePanel({ flatId }: { flatId: string }) {
  const { t, i18n } = useTranslation('home')
  const headingId = useId()
  const invite = useFlatInviteCode(flatId, true)
  const regenerate = useRegenerateFlatInviteCode(flatId)
  const [confirming, setConfirming] = useState(false)
  const [copyState, setCopyState] = useState<CopyState>('idle')
  const canShare = typeof navigator !== 'undefined' && typeof navigator.share === 'function'

  const code = invite.data?.code
  const link = code ? buildInviteLink(window.location.origin, code) : ''
  const expiresOn = invite.data
    ? new Date(invite.data.expiresAt).toLocaleDateString(i18n.language, {
        day: 'numeric',
        month: 'long',
        year: 'numeric',
      })
    : ''

  async function copyLink() {
    try {
      await navigator.clipboard.writeText(link)
      setCopyState('copied')
    } catch {
      setCopyState('failed')
    }
  }

  async function shareLink() {
    try {
      await navigator.share({ title: t('invite.shareTitle'), url: link })
    } catch {
      // The member closed the sharing sheet, or the browser refused: nothing to report.
    }
  }

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-3">
      <h2 id={headingId} className="text-lg font-semibold text-ink">
        {t('invite.heading')}
      </h2>
      {invite.isPending && <p role="status">{t('invite.loading')}</p>}
      {(invite.isError || regenerate.isError) && (
        <p role="alert" className="text-ink">
          {t('invite.error')}
        </p>
      )}
      {code && (
        <>
          <p className="text-sm text-ink-muted">{t('invite.intro')}</p>
          <div className="flex flex-col gap-1 rounded-md border border-line bg-surface-raised p-4">
            <span className="text-sm text-ink-muted">{t('invite.codeLabel')}</span>
            <span className="font-mono text-2xl tracking-widest text-ink">{code}</span>
            <span className="text-sm text-ink-muted">
              {t('invite.expires', { date: expiresOn })}
            </span>
          </div>
          <div className="flex flex-wrap gap-2">
            <button type="button" className={buttonClass} onClick={copyLink}>
              {t('invite.copy')}
            </button>
            {canShare && (
              <button type="button" className={buttonClass} onClick={shareLink}>
                {t('invite.share')}
              </button>
            )}
            <a
              href={whatsappShareHref(t('invite.whatsappMessage', { link }))}
              target="_blank"
              rel="noopener noreferrer"
              className={buttonClass}
            >
              {t('invite.whatsapp')}
            </a>
          </div>
          <p role="status" className="text-sm text-ink-muted">
            {copyState === 'copied' && t('invite.copied')}
            {copyState === 'failed' && t('invite.copyFailed')}
          </p>
          {confirming ? (
            <div role="alertdialog" className="flex flex-col gap-3">
              <p className="text-ink">{t('invite.confirmRegenerate')}</p>
              <div className="flex flex-wrap gap-2">
                <button
                  type="button"
                  className={buttonClass}
                  disabled={regenerate.isPending}
                  onClick={() =>
                    regenerate.mutate(undefined, {
                      onSettled: () => setConfirming(false),
                      onSuccess: () => setCopyState('idle'),
                    })
                  }
                >
                  {t('invite.confirm')}
                </button>
                <button type="button" className={buttonClass} onClick={() => setConfirming(false)}>
                  {t('invite.cancel')}
                </button>
              </div>
            </div>
          ) : (
            <div>
              <button
                type="button"
                className={buttonClass}
                disabled={regenerate.isPending}
                onClick={() => setConfirming(true)}
              >
                {t('invite.regenerate')}
              </button>
            </div>
          )}
        </>
      )}
    </section>
  )
}
