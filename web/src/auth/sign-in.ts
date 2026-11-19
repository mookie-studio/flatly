import { config } from '@/lib/config'

export type SignInProvider = 'google' | 'microsoft'

/**
 * Address that starts the external sign-in. The API sends the browser to the provider and, after it
 * signs the member in, back to `returnPath` on the web origin.
 */
export function signInUrl(provider: SignInProvider, returnPath: string): string {
  const url = new URL(`/api/v1/platform/auth/${provider}/sign-in`, config.apiUrl)
  url.searchParams.set('returnUrl', returnPath)
  return url.toString()
}
