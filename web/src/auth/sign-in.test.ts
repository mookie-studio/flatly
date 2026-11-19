import { config } from '@/lib/config'
import { signInUrl } from './sign-in'

describe('signInUrl', () => {
  it('starts the provider sign-in on the API and carries the return path', () => {
    const url = new URL(signInUrl('google', '/rooms/1?tab=week'))

    expect(url.origin).toBe(new URL(config.apiUrl).origin)
    expect(url.pathname).toBe('/api/v1/platform/auth/google/sign-in')
    expect(url.searchParams.get('returnUrl')).toBe('/rooms/1?tab=week')
  })

  it('uses the Microsoft route for Microsoft accounts', () => {
    expect(new URL(signInUrl('microsoft', '/')).pathname).toBe(
      '/api/v1/platform/auth/microsoft/sign-in',
    )
  })
})
